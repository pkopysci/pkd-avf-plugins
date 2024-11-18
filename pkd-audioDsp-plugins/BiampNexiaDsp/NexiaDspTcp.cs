namespace BiampNexiaDsp
{
	using Crestron.SimplSharp;
	using Crestron.SimplSharp.CrestronSockets;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_common_utils.NetComs;
	using pkd_common_utils.Validation;
	using pkd_hardware_service.AudioDevices;
	using pkd_hardware_service.BaseDevice;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;

	/// <summary>
	/// GCU C# framework plugin for controlling a Biam Nexia. Call Initialize() to begin control.
	/// </summary>
	public class NexiaDspTcp : BaseDevice, IDsp, IDisposable
	{
		private static readonly string RxPattern = @"#(?<command>.*) (?<dspId>\d+) (?<control>\w*) (?<tag>\d*) (?<index1>\d+) (?<index2>\d*) *(?<value>-*\d+)* *(?<result>\+OK|-ERR)*";
		private readonly List<NexiaAudioChannel> inputs;
		private readonly List<NexiaAudioChannel> outputs;
		private readonly List<NexiaPreset> presets;
		private readonly StringBuilder rxBuilder;
		private BasicTcpClient client;
		private CTimer sendTimer;
		private CTimer queryTimer;
		private bool isDisposed;
		private string hostname;
		private int port;
		private int errCount;
		private int coreId;

		/// <summary>
		/// Initializes a new instance of the <see cref="NexiaDsp"/> class.
		/// </summary>
		public NexiaDspTcp()
		{
			this.IsInitialized = false;
			this.Id = "NexiaDspDefaultID";
			this.Label = "Nexia DSP";
			this.inputs = new List<NexiaAudioChannel>();
			this.outputs = new List<NexiaAudioChannel>();
			this.presets = new List<NexiaPreset>();
			this.rxBuilder = new StringBuilder();
		}

		~NexiaDspTcp()
		{
			this.Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioInputLevelChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioInputMuteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioOutputLevelChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioOutputMuteChanged;

		/// <inheritdoc/>
		public IEnumerable<string> GetPresetIds
		{
			get
			{
				List<string> ids = new List<string>();
				foreach (var preset in this.presets)
				{
					ids.Add(preset.Id);
				}

				return ids;
			}
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetInputIds
		{
			get
			{
				List<string> ids = new List<string>();
				foreach (var input in this.inputs)
				{
					ids.Add(input.Id);
				}

				return ids;
			}
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetOutputIds
		{
			get
			{
				List<string> ids = new List<string>();
				foreach (var output in this.outputs)
				{
					ids.Add(output.Id);
				}

				return ids;
			}
		}

		/// <inheritdoc/>
		public override bool IsInitialized { get; protected set; }

		/// <inheritdoc/>
		public void Initialize(string hostId, int coreId, string hostname, int port, string username, string password)
		{
			ParameterValidator.ThrowIfNullOrEmpty(hostId, "Initialize", "hostId");
			ParameterValidator.ThrowIfNullOrEmpty(hostname, "Initialize", "hostname");
			ParameterValidator.ThrowIfNull(username, "Initialize", "username");
			ParameterValidator.ThrowIfNull(password, "Initialize", "password");

			this.IsInitialized = false;

			this.Id = hostId;
			this.coreId = coreId;
			this.hostname = hostname;
			this.port = port;

			//this.client = new TcpClientManager(this.hostname, this.port, @".*?\r\n");
			this.client = new BasicTcpClient(this.hostname, this.port, 1024)
			{
				EnableReconnect = true
			};
			this.SubscribeClient();
			this.IsOnline = false;

			this.IsInitialized = true;
		}

		/// <inheritdoc/>
		public override void Connect()
		{
			if (!this.IsInitialized)
			{
				Logger.Error("NexiaDspTcp {0} - Connect() : Run Initialize() first.", this.Id);
				return;
			}

			this.client.Connect();
		}

		/// <inheritdoc/>
		public override void Disconnect()
		{
			if (this.client != null)
			{
				this.client.Disconnect();
				this.errCount = 0;
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc/>
		public void AddInputChannel(string id, string levelTag, string muteTag, int index, int levelMax, int levelMin, int routerIndex)
		{
			ParameterValidator.ThrowIfNullOrEmpty(muteTag, "AddInputChannel", "muteTag");
			ParameterValidator.ThrowIfNullOrEmpty(levelTag, "AddInputChannel", "levelTag");

			var current = this.inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (current != null)
			{
				current.AudioVolumeChanged -= this.InputControlLevelChange;
				current.AudioMuteChanged -= this.InputControlMuteChange;
				current.QueueCommand -= this.TrySend;
				this.inputs.Remove(current);
			}

			NexiaAudioChannel channel = new NexiaAudioChannel(
				this.coreId,
				id,
				muteTag,
				levelTag,
				new string[] { },
				index,
				levelMax,
				levelMin);

			channel.AudioMuteChanged += this.InputControlMuteChange;
			channel.AudioVolumeChanged += this.InputControlLevelChange;
			channel.QueueCommand += this.TrySend;
			this.inputs.Add(channel);
		}

		/// <inheritdoc/>
		public void AddOutputChannel(string id, string levelTag, string muteTag, string routerTag, int routerIndex, int index, int levelMax, int levelMin)
		{
			ParameterValidator.ThrowIfNullOrEmpty(muteTag, "AddInputChannel", "muteTag");
			ParameterValidator.ThrowIfNullOrEmpty(levelTag, "AddInputChannel", "levelTag");

			var current = this.outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (current != null)
			{
				current.AudioVolumeChanged -= this.OutputControlMuteChange;
				current.AudioMuteChanged -= this.OutputControlLevelChange;
				current.QueueCommand -= this.TrySend;
				this.outputs.Remove(current);
			}

			var channel = new NexiaAudioChannel(
				this.coreId,
				id,
				muteTag,
				levelTag,
				new String[] { },
				index,
				levelMax,
				levelMin);

			channel.AudioMuteChanged += this.OutputControlMuteChange;
			channel.AudioVolumeChanged += this.OutputControlLevelChange;
			channel.QueueCommand += this.TrySend;
			this.outputs.Add(channel);
		}

		/// <inheritdoc/>
		public void AddPreset(string id, int index)
		{
			var current = this.presets.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (current != null)
			{
				current.QueueCommand -= this.TrySend;
				this.presets.Remove(current);
			}

			var newPreset = new NexiaPreset(this.coreId, id, index);
			newPreset.QueueCommand += this.TrySend;
			this.presets.Add(newPreset);
		}

		/// <inheritdoc/>
		public int GetAudioInputLevel(string id)
		{
			NexiaAudioChannel found = this.inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				return found.AudioLevel;
			}

			Logger.Warn("NexiaDspTcp {0} - GetInputLevel() : Unable to find input channel with ID {1}", this.Id, id);
			return 0;
		}

		/// <inheritdoc/>
		public bool GetAudioInputMute(string id)
		{
			NexiaAudioChannel found = this.inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				return found.AudioMute;
			}

			Logger.Warn("NexiaDspTcp {0} - GetInputMute() : Unable to find input channel with ID {1}", this.Id, id);
			return false;
		}

		/// <inheritdoc/>
		public int GetAudioOutputLevel(string id)
		{
			NexiaAudioChannel found = this.outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				return found.AudioLevel;
			}

			Logger.Warn("NexiaDspTcp {0} - GetOutputLevel() : Unable to find output channel with ID {1}", this.Id, id);
			return 0;
		}

		/// <inheritdoc/>
		public bool GetAudioOutputMute(string id)
		{
			NexiaAudioChannel found = this.outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				return found.AudioMute;
			}

			Logger.Warn("NexiaDspTcp {0} - GetOutputMute() : Unable to find output channel with ID {1}", this.Id, id);
			return false;
		}

		/// <inheritdoc/>
		public void RecallAudioPreset(string id)
		{
			var found = this.presets.FirstOrDefault(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			found?.RecallPreset();
		}

		/// <inheritdoc/>
		public void SetAudioInputLevel(string id, int level)
		{
			Logger.Debug("NexiaDspTcp {0} - SetAudioInputLevel({1}, {2})", this.Id, id, level);

			NexiaAudioChannel found = this.inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				found.SetAudioLevel(level);
			}
			else
			{
				Logger.Warn("NexiaDspTcp {0} - SetInputLevel() : Unable to find input channel with ID {1}", this.Id, id);
			}
		}

		/// <inheritdoc/>
		public void SetAudioInputMute(string id, bool mute)
		{
			NexiaAudioChannel found = this.inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				found.SetAudioMute(mute);
			}
			else
			{
				Logger.Warn("NexiaDspTcp {0} - SetInputMute() : Unable to find input channel with ID {1}", this.Id, id);
			}
		}

		/// <inheritdoc/>
		public void SetAudioOutputLevel(string id, int level)
		{
			NexiaAudioChannel found = this.outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				found.SetAudioLevel(level);
			}
			else
			{
				Logger.Warn("NexiaDspTcp {0} - SetOutputLevel() : Unable to find output channel with ID {1}", this.Id, id);
			}
		}

		/// <inheritdoc/>
		public void SetAudioOutputMute(string id, bool mute)
		{
			NexiaAudioChannel found = this.outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				found.SetAudioMute(mute);
			}
			else
			{
				Logger.Warn("NexiaDspTcp {0} - SetOutputMute() : Unable to find output channel with ID {1}", this.Id, id);
			}
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioOutputIds()
		{
			List<string> ids = new List<string>();
			foreach (var output in this.outputs)
			{
				ids.Add(output.Id);
			}

			return ids;
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioInputIds()
		{
			List<string> ids = new List<string>();
			foreach (var input in this.inputs)
			{
				ids.Add(input.Id);
			}

			return ids;
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioPresetIds()
		{
			List<string> ids = new List<string>();
			foreach (var preset in this.presets)
			{
				ids.Add(preset.Id);
			}

			return ids;
		}

		private void Dispose(bool disposing)
		{
			if (!this.isDisposed)
			{
				if (disposing)
				{
					if (this.client != null)
					{
						this.UnsubscribeClient();
						this.client.Disconnect();
						this.client.Dispose();
					}

					if (this.sendTimer != null)
					{
						this.sendTimer.Dispose();
						this.sendTimer = null;
					}
				}

				this.isDisposed = true;
			}
		}

		private void TrySend(string command)
		{
			if (!this.IsOnline)
			{
				return;
			}

			this.client.Send(command);
			if (this.sendTimer == null)
			{
				this.sendTimer = new CTimer(this.SendTimeoutcallback, 1000);
			}
			else
			{
				this.sendTimer.Reset(1000);
			}
		}

		private void SendTimeoutcallback(object obj)
		{
			Logger.Error("NexiaDspTcp {0} - No response after sending command. Attempting reconnect...", this.Id);
			this.client.Disconnect();
			this.client.Connect();
		}

		private void InputControlMuteChange(object sender, GenericDualEventArgs<string, int> e)
		{
			this.Notify(this.AudioInputMuteChanged, e.Arg1);
		}

		private void InputControlLevelChange(object sender, GenericDualEventArgs<string, int> e)
		{
			this.Notify(this.AudioInputLevelChanged, e.Arg1);
		}

		private void OutputControlMuteChange(object sender, GenericDualEventArgs<string, int> e)
		{
			this.Notify(this.AudioOutputMuteChanged, e.Arg1);
		}

		private void OutputControlLevelChange(object sender, GenericDualEventArgs<string, int> e)
		{
			this.Notify(this.AudioOutputLevelChanged, e.Arg1);
		}

		private void Notify(EventHandler<GenericDualEventArgs<string, string>> handler, string arg)
		{
			var temp = handler;
			handler?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, arg));
		}

		private void SubscribeClient()
		{
			if (this.client != null)
			{
				this.client.ConnectionFailed += this.ClientConnectFailedHandler;
				this.client.ClientConnected += this.ClientConnectedHandler;
				this.client.StatusChanged += this.ClientStatusChangedHandler;
				this.client.RxBytesRecieved += this.ClientBytesReceived;
			}
		}

		private void UnsubscribeClient()
		{
			if (this.client != null)
			{
				this.client.ConnectionFailed -= this.ClientConnectFailedHandler;
				this.client.ClientConnected -= this.ClientConnectedHandler;
				this.client.StatusChanged -= this.ClientStatusChangedHandler;
				this.client.RxBytesRecieved -= this.ClientBytesReceived;
			}
		}

		private void ClientBytesReceived(object sender, GenericSingleEventArgs<byte[]> e)
		{
			this.sendTimer?.Stop();

			if (e.Arg.Length <= 0)
			{
				return;
			}

			string converted = Encoding.GetEncoding("ISO-8859-1").GetString(e.Arg, 0, e.Arg.Length);
			rxBuilder.Append(converted);
			string currentStream = this.rxBuilder.ToString();

			var matches = Regex.Matches(currentStream, @".*\r\n");
			foreach (Match match in matches)
			{
				this.ParseResponse(match.Value);

				try
				{
					string data = rxBuilder.ToString();
					int matchPos = data.IndexOf(match.Value, StringComparison.InvariantCulture);
					rxBuilder.Remove(matchPos, match.Length);
				}
				catch (Exception ex)
				{
					Logger.Error("{0}-{1}", ex.Message, ex.StackTrace);
				}
			}
		}

		private void ParseResponse(string rx)
		{
			Match match = Regex.Match(rx, RxPattern);
			if (match.Success)
			{
				string result = match.Groups["result"].Value.ToUpper();
				if (result.Contains("ERR"))
				{
					Logger.Error("NexiaDspTcp {0} - Error Rx Received from device: {1}", this.Id, rx);
					return;
				}

				string control = match.Groups["control"].Value.ToUpper();
				if (control.Equals(NexiaComander.Blocks[NexiaBlocks.FaderLevel]))
				{
					this.ParseLevelResponse(match);
				}
				else if (control.Equals(NexiaComander.Blocks[NexiaBlocks.FaderMute]))
				{
					this.ParseMuteResponse(match);
				}
				else if (control.Equals(NexiaComander.Blocks[NexiaBlocks.Preset]))
				{
					// Notify preset ACK?
				}
				else
				{
					Logger.Error("NexiaDspTcp {0} - Unknown control response received: {1}", this.Id, rx);
				}
			}
		}

		private bool TryConvertLevel(string value, out int percent, int levelMin, int levelMax)
		{
			try
			{
				percent = NexiaComander.FloatToPercent(float.Parse(value), levelMin, levelMax);
				return true;
			}
			catch (FormatException err)
			{
				Logger.Error(err, "NexiaDspTcp {0} - failed to parse level value of {1}", this.Id, value);
			}
			catch (ArgumentNullException)
			{
				Logger.Error("NexiaDspTcp {0} - Level change null data for value, cannot parse.", this.Id);
			}

			percent = 0;
			return false;
		}

		private bool UpdateOutputLevel(string instanceTag, string index, string rawValue)
		{
			int idx = 0;
			try
			{
				idx = int.Parse(index);
			}
			catch (Exception e)
			{
				Logger.Error(e, "NexiaDspTcp {0} - UpdateOutputLevel() - Failed to parse index number {0} for input {1}", this.Id, index, instanceTag);
				return false;
			}

			NexiaAudioChannel found = this.outputs.Find(x => x.LevelTag.Equals(instanceTag, StringComparison.InvariantCulture));
			if (found != null)
			{
				if (this.TryConvertLevel(rawValue, out int percent, found.LevelMin, found.LevelMax))
				{
					found.AudioLevel = percent;
					return true;
				}
			}

			return false;
		}

		private bool UpdateInputLevel(string instanceTag, string index, string rawValue)
		{
			int idx = 0;
			try
			{
				idx = int.Parse(index);
			}
			catch (Exception e)
			{
				Logger.Error(e, "NexiaDspTcp {0} - UpdateInputLevel() - Failed to parse index number {0} for input {1}", this.Id, index, instanceTag);
				return false;
			}

			NexiaAudioChannel found = this.inputs.Find(x => x.LevelTag.Equals(instanceTag, StringComparison.InvariantCulture) && x.Index == idx);
			if (found != null)
			{
				if (this.TryConvertLevel(rawValue, out int percent, found.LevelMin, found.LevelMax))
				{
					found.AudioLevel = percent;
					return true;
				}
			}

			return false;
		}

		private void ParseLevelResponse(Match match)
		{
			Logger.Debug("ParseLevelResponse({0})", match.Value);

			string level = match.Groups["value"].Value;
			string tag = match.Groups["tag"].Value;
			string index = match.Groups["index1"].Value;

			if (string.IsNullOrEmpty(level))
			{
				level = match.Groups["index2"].Value;
			}

			bool isOutput = this.UpdateOutputLevel(tag, index, level);
			bool isInput = this.UpdateInputLevel(tag, index, level);

			if (!isOutput && !isInput)
			{
				Logger.Error(
					"NexiaDspTcp {0} - Cannot find input or output channel with tag {1}",
					this.Id,
					match.Groups["tag"].Value);
			}
		}

		private void ParseMuteResponse(Match match)
		{
			string instanceTag = match.Groups["tag"].Value;
			bool muted = false;

			int idx = 0;
			try
			{
				idx = int.Parse(match.Groups["index1"].Value);
			}
			catch (Exception e)
			{
				Logger.Error(
					e,
					"NexiaDspTcp {0} - ParseMuteResponse() - Failed to parse index number {0} for input {1}",
					this.Id,
					match.Groups["index1"].Value,
					instanceTag);
				return;
			}

			// If there is no value returned by the device then data will be in the "index2" regex group.
			if (string.IsNullOrEmpty(match.Groups["value"].Value))
			{
				muted = match.Groups["index2"].Value.Equals("1", StringComparison.InvariantCulture);
			}
			else
			{
				muted = match.Groups["value"].Value.Equals("1", StringComparison.InvariantCulture);
			}


			NexiaAudioChannel found = this.outputs.Find(x => x.MuteTag.Equals(instanceTag, StringComparison.InvariantCulture) && x.Index == idx);
			if (found != null)
			{
				found.AudioMute = muted;
			}
			else
			{
				found = this.inputs.FirstOrDefault(x => x.MuteTag.Equals(instanceTag, StringComparison.InvariantCulture) && x.Index == idx);
				if (found != null)
				{
					found.AudioMute = muted;
				}
				else
				{
					Logger.Error("NexiaDspTcp {0} - No channel found with mute instance tag {instanceTag}", this.Id);
				}
			}
		}

		private void ClientStatusChangedHandler(object sender, EventArgs e)
		{
			this.ClientConnectedHandler(sender, e);
		}

		private void ClientConnectFailedHandler(object sender, GenericSingleEventArgs<SocketStatus> e)
		{
			if (this.errCount <= 9)
			{
				Logger.Error("NexiaDspTcp {0} - Failed to establish a connection.");
			}
			else if (this.errCount == 10)
			{
				Logger.Error("NexiaDspTcp {0} - 10 failed attempts to establish a connection. Silencing notice until connected.");
			}

			this.errCount++;
		}

		private void ClientConnectedHandler(object sender, EventArgs e)
		{
			this.IsOnline = this.client.Connected;
			if (this.IsOnline)
			{
				this.errCount = 0;
			}

			this.NotifyOnlineStatus();
			this.SendStatusQuery();
			if (this.queryTimer == null)
			{
				this.queryTimer = new CTimer(this.QueryCallback, 30000);
			}
			else
			{
				this.queryTimer.Reset(30000);
			}
		}

		private void QueryCallback(object obj)
		{
			if (this.IsOnline)
			{
				this.SendStatusQuery();
				this.queryTimer.Reset(30000);
			}
		}

		private void SendStatusQuery()
		{
			foreach (var input in this.inputs)
			{
				input.QueryLevel();
				input.QueryMute();
			}

			foreach (var output in this.outputs)
			{
				output.QueryMute();
				output.QueryLevel();
			}
		}
	}
}
