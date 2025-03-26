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
	/// GCU C# framework plugin for controlling a Biamp Nexia. Call Initialize() to begin control.
	/// </summary>
	public class NexiaDspTcp : BaseDevice, IDsp
	{
		private const string RxPattern = @"#(?<command>.*) (?<dspId>\d+) (?<control>\w*) (?<tag>\d*) (?<index1>\d+) (?<index2>\d*) *(?<value>-*\d+)* *(?<result>\+OK|-ERR)*";
		private readonly List<NexiaAudioChannel> _inputs = [];
		private readonly List<NexiaAudioChannel> _outputs = [];
		private readonly List<NexiaPreset> _presets = [];
		private readonly StringBuilder _rxBuilder;
		private BasicTcpClient? _client;
		private CTimer? _sendTimer;
		private CTimer? _queryTimer;
		private bool _isDisposed;
		private int _coreId;
		private int _errCount;

		/// <summary>
		/// Initializes a new instance of the <see cref="NexiaDspTcp"/> class.
		/// </summary>
		public NexiaDspTcp()
		{
			Id = "NexiaDspDefaultID";
			Label = "Nexia DSP";
			_rxBuilder = new StringBuilder();

			Manufacturer = "Biamp";
			Model = "Nexia";
		}

		~NexiaDspTcp()
		{
			Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputLevelChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputMuteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputLevelChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputMuteChanged;

		/// <summary>
		/// Gets a collection of ids for all presets that have been added using the AddPreset() method.
		/// </summary>
		public IEnumerable<string> GetPresetIds
		{
			get
			{
				List<string> ids = [];
				foreach (var preset in _presets)
				{
					ids.Add(preset.Id);
				}

				return ids;
			}
		}

		/// <summary>
		/// Gets a collection of ids for all inputs added to this object.
		/// </summary>
		public IEnumerable<string> GetInputIds
		{
			get
			{
				List<string> ids = [];
				foreach (var input in _inputs)
				{
					ids.Add(input.Id);
				}

				return ids;
			}
		}

		/// <summary>
		/// Gets a collection of ids for all outputs added to this object.
		/// </summary>
		public IEnumerable<string> GetOutputIds
		{
			get
			{
				List<string> ids = [];
				foreach (var output in _outputs)
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

			IsInitialized = false;

			Id = hostId;
			_coreId = coreId;
			_client = new BasicTcpClient(hostname, port, 1024)
			{
				EnableReconnect = true
			};
			
			SubscribeClient();
			IsOnline = false;

			IsInitialized = true;
		}

		/// <inheritdoc/>
		public override void Connect()
		{
			if (!IsInitialized)
			{
				Logger.Error("NexiaDspTcp {0} - Connect() : Run Initialize() first.", Id);
				return;
			}

			_client?.Connect();
		}

		/// <inheritdoc/>
		public override void Disconnect()
		{
			if (!IsInitialized)
			{
				Logger.Error($"NexiaDspTcp {Id} - Disconnect() - device not initialized.");
				return;
			}
			
			_client?.Disconnect();
			_errCount = 0;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc/>
		public void AddInputChannel(string id, string levelTag, string muteTag, int index, int levelMax, int levelMin, int routerIndex, List<string> _)
		{
			ParameterValidator.ThrowIfNullOrEmpty(muteTag, "AddInputChannel", nameof(muteTag));
			ParameterValidator.ThrowIfNullOrEmpty(levelTag, "AddInputChannel", nameof(levelTag));

			var current = _inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (current != null)
			{
				current.AudioVolumeChanged -= InputControlLevelChange;
				current.AudioMuteChanged -= InputControlMuteChange;
				current.QueueCommand -= TrySend;
				_inputs.Remove(current);
			}

			var channel = new NexiaAudioChannel(
				_coreId,
				id,
				muteTag,
				levelTag,
				[],
				index,
				levelMax,
				levelMin);

			channel.AudioMuteChanged += InputControlMuteChange;
			channel.AudioVolumeChanged += InputControlLevelChange;
			channel.QueueCommand += TrySend;
			_inputs.Add(channel);
		}

		/// <inheritdoc/>
		public void AddOutputChannel(string id, string levelTag, string muteTag, string routerTag, int routerIndex, int index, int levelMax, int levelMin, List<string> _)
		{
			ParameterValidator.ThrowIfNullOrEmpty(muteTag, "AddInputChannel", nameof(muteTag));
			ParameterValidator.ThrowIfNullOrEmpty(levelTag, "AddInputChannel", nameof(levelTag));

			var current = _outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (current != null)
			{
				current.AudioVolumeChanged -= OutputControlMuteChange;
				current.AudioMuteChanged -= OutputControlLevelChange;
				current.QueueCommand -= TrySend;
				_outputs.Remove(current);
			}

			var channel = new NexiaAudioChannel(
				_coreId,
				id,
				muteTag,
				levelTag,
				[],
				index,
				levelMax,
				levelMin);

			channel.AudioMuteChanged += OutputControlMuteChange;
			channel.AudioVolumeChanged += OutputControlLevelChange;
			channel.QueueCommand += TrySend;
			_outputs.Add(channel);
		}

		/// <inheritdoc/>
		public void AddPreset(string id, int index)
		{
			var current = _presets.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (current != null)
			{
				current.QueueCommand -= TrySend;
				_presets.Remove(current);
			}

			var newPreset = new NexiaPreset(_coreId, id, index);
			newPreset.QueueCommand += TrySend;
			_presets.Add(newPreset);
		}

		/// <inheritdoc/>
		public int GetAudioInputLevel(string id)
		{
			var found = _inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				return found.AudioLevel;
			}

			Logger.Warn("NexiaDspTcp {0} - GetInputLevel() : Unable to find input channel with ID {1}", Id, id);
			return 0;
		}

		/// <inheritdoc/>
		public bool GetAudioInputMute(string id)
		{
			var found = _inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				return found.AudioMute;
			}

			Logger.Warn("NexiaDspTcp {0} - GetInputMute() : Unable to find input channel with ID {1}", Id, id);
			return false;
		}

		/// <inheritdoc/>
		public int GetAudioOutputLevel(string id)
		{
			var found = _outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				return found.AudioLevel;
			}

			Logger.Warn("NexiaDspTcp {0} - GetOutputLevel() : Unable to find output channel with ID {1}", Id, id);
			return 0;
		}

		/// <inheritdoc/>
		public bool GetAudioOutputMute(string id)
		{
			var found = _outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				return found.AudioMute;
			}

			Logger.Warn("NexiaDspTcp {0} - GetOutputMute() : Unable to find output channel with ID {1}", Id, id);
			return false;
		}

		/// <inheritdoc/>
		public void RecallAudioPreset(string id)
		{
			var found = _presets.FirstOrDefault(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			found?.RecallPreset();
		}

		/// <inheritdoc/>
		public void SetAudioInputLevel(string id, int level)
		{
			Logger.Debug("NexiaDspTcp {0} - SetAudioInputLevel({1}, {2})", Id, id, level);

			var found = _inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				found.SetAudioLevel(level);
			}
			else
			{
				Logger.Warn("NexiaDspTcp {0} - SetInputLevel() : Unable to find input channel with ID {1}", Id, id);
			}
		}

		/// <inheritdoc/>
		public void SetAudioInputMute(string id, bool mute)
		{
			var found = _inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				found.SetAudioMute(mute);
			}
			else
			{
				Logger.Warn("NexiaDspTcp {0} - SetInputMute() : Unable to find input channel with ID {1}", Id, id);
			}
		}

		/// <inheritdoc/>
		public void SetAudioOutputLevel(string id, int level)
		{
			var found = _outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				found.SetAudioLevel(level);
			}
			else
			{
				Logger.Warn("NexiaDspTcp {0} - SetOutputLevel() : Unable to find output channel with ID {1}", Id, id);
			}
		}

		/// <inheritdoc/>
		public void SetAudioOutputMute(string id, bool mute)
		{
			var found = _outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found != null)
			{
				found.SetAudioMute(mute);
			}
			else
			{
				Logger.Warn("NexiaDspTcp {0} - SetOutputMute() : Unable to find output channel with ID {1}", Id, id);
			}
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioOutputIds()
		{
			List<string> ids = [];
			foreach (var output in _outputs)
			{
				ids.Add(output.Id);
			}

			return ids;
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioInputIds()
		{
			List<string> ids = new List<string>();
			foreach (var input in _inputs)
			{
				ids.Add(input.Id);
			}

			return ids;
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioPresetIds()
		{
			List<string> ids = new List<string>();
			foreach (var preset in _presets)
			{
				ids.Add(preset.Id);
			}

			return ids;
		}

		private void Dispose(bool disposing)
		{
			if (!_isDisposed)
			{
				if (disposing)
				{
					if (_client != null)
					{
						UnsubscribeClient();
						_client.Disconnect();
						_client.Dispose();
					}

					if (_sendTimer != null)
					{
						_sendTimer.Dispose();
						_sendTimer = null;
					}
				}

				_isDisposed = true;
			}
		}

		private void TrySend(string command)
		{
			if (!IsOnline)
			{
				return;
			}

			_client?.Send(command);
			if (_sendTimer == null)
			{
				_sendTimer = new CTimer(SendTimeoutCallback, 1000);
			}
			else
			{
				_sendTimer.Reset(1000);
			}
		}

		private void SendTimeoutCallback(object? obj)
		{
			Logger.Error("NexiaDspTcp {0} - No response after sending command. Attempting reconnect...", Id);
			_client?.Disconnect();
			_client?.Connect();
		}

		private void InputControlMuteChange(object? sender, GenericDualEventArgs<string, int> e)
		{
			Notify(AudioInputMuteChanged, e.Arg1);
		}

		private void InputControlLevelChange(object? sender, GenericDualEventArgs<string, int> e)
		{
			Notify(AudioInputLevelChanged, e.Arg1);
		}

		private void OutputControlMuteChange(object? sender, GenericDualEventArgs<string, int> e)
		{
			Notify(AudioOutputMuteChanged, e.Arg1);
		}

		private void OutputControlLevelChange(object? sender, GenericDualEventArgs<string, int> e)
		{
			Notify(AudioOutputLevelChanged, e.Arg1);
		}

		private void Notify(EventHandler<GenericDualEventArgs<string, string>>? handler, string arg)
		{
			handler?.Invoke(this, new GenericDualEventArgs<string, string>(Id, arg));
		}

		private void SubscribeClient()
		{
			if (_client == null) return;
			_client.ConnectionFailed += ClientConnectFailedHandler;
			_client.ClientConnected += ClientConnectedHandler;
			_client.StatusChanged += ClientStatusChangedHandler;
			_client.RxBytesReceived += ClientBytesReceived;
		}

		private void UnsubscribeClient()
		{
			if (_client == null) return;
			_client.ConnectionFailed -= ClientConnectFailedHandler;
			_client.ClientConnected -= ClientConnectedHandler;
			_client.StatusChanged -= ClientStatusChangedHandler;
			_client.RxBytesReceived -= ClientBytesReceived;
		}

		private void ClientBytesReceived(object? sender, GenericSingleEventArgs<byte[]> e)
		{
			_sendTimer?.Stop();

			if (e.Arg.Length <= 0)
			{
				return;
			}

			string converted = Encoding.GetEncoding("ISO-8859-1").GetString(e.Arg, 0, e.Arg.Length);
			_rxBuilder.Append(converted);
			string currentStream = _rxBuilder.ToString();

			var matches = Regex.Matches(currentStream, @".*\r\n");
			foreach (Match match in matches)
			{
				ParseResponse(match.Value);

				try
				{
					var data = _rxBuilder.ToString();
					var matchPos = data.IndexOf(match.Value, StringComparison.InvariantCulture);
					_rxBuilder.Remove(matchPos, match.Length);
				}
				catch (Exception ex)
				{
					Logger.Error("{0}-{1}", ex.Message, ex.StackTrace ?? string.Empty);
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
					Logger.Error("NexiaDspTcp {0} - Error Rx Received from device: {1}", Id, rx);
					return;
				}

				string control = match.Groups["control"].Value.ToUpper();
				if (control.Equals(NexiaCommander.Blocks[NexiaBlocks.FaderLevel]))
				{
					ParseLevelResponse(match);
				}
				else if (control.Equals(NexiaCommander.Blocks[NexiaBlocks.FaderMute]))
				{
					ParseMuteResponse(match);
				}
				else if (control.Equals(NexiaCommander.Blocks[NexiaBlocks.Preset]))
				{
					// Notify preset ACK?
				}
				else
				{
					Logger.Error("NexiaDspTcp {0} - Unknown control response received: {1}", Id, rx);
				}
			}
		}

		private bool TryConvertLevel(string value, out int percent, int levelMin, int levelMax)
		{
			try
			{
				percent = NexiaCommander.FloatToPercent(float.Parse(value), levelMin, levelMax);
				return true;
			}
			catch (FormatException err)
			{
				Logger.Error(err, "NexiaDspTcp {0} - failed to parse level value of {1}", Id, value);
			}
			catch (ArgumentNullException)
			{
				Logger.Error("NexiaDspTcp {0} - Level change null data for value, cannot parse.", Id);
			}

			percent = 0;
			return false;
		}

		private bool UpdateOutputLevel(string instanceTag, string index, string rawValue)
		{
			int idx;
			try
			{
				idx = int.Parse(index);
			}
			catch (Exception e)
			{
				Logger.Error(e, "NexiaDspTcp {0} - UpdateInputLevel() - Failed to parse index number {0} for input {1}", Id, index, instanceTag);
				return false;
			}
			
			var found = _outputs.Find(x => x.LevelTag.Equals(instanceTag, StringComparison.InvariantCulture) && x.Index == idx);
			if (found == null) return false;
			if (!TryConvertLevel(rawValue, out var percent, found.LevelMin, found.LevelMax)) return false;
			found.AudioLevel = percent;
			return true;
		}

		private bool UpdateInputLevel(string instanceTag, string index, string rawValue)
		{
			int idx;
			try
			{
				idx = int.Parse(index);
			}
			catch (Exception e)
			{
				Logger.Error(e, "NexiaDspTcp {0} - UpdateInputLevel() - Failed to parse index number {0} for input {1}", Id, index, instanceTag);
				return false;
			}

			var found = _inputs.Find(x => x.LevelTag.Equals(instanceTag, StringComparison.InvariantCulture) && x.Index == idx);
			if (found == null) return false;
			if (!TryConvertLevel(rawValue, out var percent, found.LevelMin, found.LevelMax)) return false;
			found.AudioLevel = percent;
			return true;

		}

		private void ParseLevelResponse(Match match)
		{
			Logger.Debug("ParseLevelResponse({0})", match.Value);

			var level = match.Groups["value"].Value;
			var tag = match.Groups["tag"].Value;
			var index = match.Groups["index1"].Value;

			if (string.IsNullOrEmpty(level))
			{
				level = match.Groups["index2"].Value;
			}

			bool isOutput = UpdateOutputLevel(tag, index, level);
			bool isInput = UpdateInputLevel(tag, index, level);

			if (!isOutput && !isInput)
			{
				Logger.Error(
					"NexiaDspTcp {0} - Cannot find input or output channel with tag {1}",
					Id,
					match.Groups["tag"].Value);
			}
		}

		private void ParseMuteResponse(Match match)
		{
			var instanceTag = match.Groups["tag"].Value;
			int idx;
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
			var muted = string.IsNullOrEmpty(match.Groups["value"].Value) ?
				match.Groups["index2"].Value.Equals("1", StringComparison.InvariantCulture)
				: match.Groups["value"].Value.Equals("1", StringComparison.InvariantCulture);


			var found = _outputs.Find(x => x.MuteTag.Equals(instanceTag, StringComparison.InvariantCulture) && x.Index == idx);
			if (found != null)
			{
				found.AudioMute = muted;
			}
			else
			{
				found = _inputs.FirstOrDefault(x =>
					x.MuteTag.Equals(instanceTag, StringComparison.InvariantCulture) && x.Index == idx);
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

		private void ClientStatusChangedHandler(object? sender, EventArgs e)
		{
			ClientConnectedHandler(sender, e);
		}

		private void ClientConnectFailedHandler(object? sender, GenericSingleEventArgs<SocketStatus> e)
		{
			if (_errCount <= 9)
			{
				Logger.Error("NexiaDspTcp {0} - Failed to establish a connection.");
			}
			else if (_errCount == 10)
			{
				Logger.Error("NexiaDspTcp {0} - 10 failed attempts to establish a connection. Silencing notice until connected.");
			}

			_errCount++;
		}

		private void ClientConnectedHandler(object? sender, EventArgs e)
		{
			IsOnline = _client?.Connected ?? false;
			if (IsOnline)
			{
				_errCount = 0;
			}

			NotifyOnlineStatus();
			SendStatusQuery();
			if (_queryTimer == null)
			{
				_queryTimer = new CTimer(QueryCallback, 30000);
			}
			else
			{
				_queryTimer.Reset(30000);
			}
		}

		private void QueryCallback(object? obj)
		{
			if (!IsOnline) return;
			SendStatusQuery();
			_queryTimer?.Reset(30000);
		}

		private void SendStatusQuery()
		{
			foreach (var input in _inputs)
			{
				input.QueryLevel();
				input.QueryMute();
			}

			foreach (var output in _outputs)
			{
				output.QueryMute();
				output.QueryLevel();
			}
		}
	}
}
