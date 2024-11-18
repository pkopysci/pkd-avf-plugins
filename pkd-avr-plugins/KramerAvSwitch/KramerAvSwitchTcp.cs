namespace KramerAvSwitch
{
	using Crestron.SimplSharp;
	using Crestron.SimplSharp.CrestronSockets;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_common_utils.NetComs;
	using pkd_common_utils.Validation;
	using pkd_hardware_service.AudioDevices;
	using pkd_hardware_service.AvSwitchDevices;
	using pkd_hardware_service.BaseDevice;
	using pkd_hardware_service.DisplayDevices;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;

	/// <summary>
	/// GCU C# framework plugin for controlling a Kramer P3000 protocol AV switcher.
	/// </summary>
	public class KramerAvSwitchTcp : BaseDevice, IAvSwitcher, IDisposable, IVideoControllable, IAudioControl
	{
		private static readonly int RX_TIMEOUT_LENGTH = 3000;
		private static readonly int POLL_TIME = 15000;
		private static readonly uint VIDEO_OUTPUT_NUMBER = 1;
		private static readonly uint MIC_NUMBER = 0;
		private static readonly string ID_RX = @"~(?<id>\d{2})@ OK\r\n";
		private static readonly string ROUTE_RX = @"~(?<id>\d{2})@ROUTE (?<layer>\d+),(?<output>\d+),(?<input>\d)+";
		private static readonly string FREEZE_RX = @"~(?<id>\d{2})@VFRZ (?<output>\d+),(?<flag>\d)";
		private static readonly string VMUTE_RX = @"~(?<id>\d{2})@VMUTE (?<output>\d+),(?<flag>\d)";
		private static readonly string AMUTE_RX = @"~(?<id>\d{2})@MUTE (?<output>\d+),(?<state>\d)";
		private static readonly string ALEVEL_RX = @"~(?<id>\d{2})@AUD-LVL (?<mode>\d+),(?<index>\d+),(?<level>\d+)";
		private static readonly string MICLEVEL_RX = @"~(?<id>\d{2})@MIC-GAIN (?<micId>\d+),(?<level>\d+)";
		private static readonly string ERR_RX = @"~(?<id>\d{2})@ERR (?<err>\d+)";
		private readonly Dictionary<string, Action<string>> supportedResponses;
		private readonly Queue<string> cmdQueue;
		private readonly List<KramerAvChannel> avOutputs;
		private readonly List<KramerAvChannel> avInputs;
		private readonly List<KramerAvChannel> mics;
		private BasicTcpClient client;
		private CTimer rxTimer;
		private CTimer pollTimer;
		private short devId;
		private bool disposed;
		private int numInputs;
		private int numOutputs;
		private bool sending;

		private string hostname;
		private int port;
		private static readonly int bufferSize = 1024;

		private static readonly string[] ERR_CODES =
		{
			"P3K_NO_ERROR",
			"ERR_PROTOCOL_SYNTAX",
			"ERR_COMMAND_NOT_AVAILABLE",
			"ERR_PARAMETER_OUT_OF_RANGE",
			"ERR_UNAUTHORIZED_ACCESS",
			"ERR_INTERNAL_FW_ERROR",
			"ERR_BUSY",
			"ERR_WRONG_CRC",
			"ERR_TIMEDOUT",
			"ERR_RESERVED",
			"ERR_FW_NOT_ENOUGH_SPACE",
			"ERR_FS_NOT_ENOUGH_SPACE",
			"ERR_FS_FILE_NOT_EXISTS",
			"ERR_FS_FILE_CANT_CREATED",
			"ERR_FS_FILE_CANT_OPEN",
			"ERR_FEATURE_NOT_SUPPORTED",
			"ERR_RESERVED_2",
			"ERR_RESERVED_3",
			"ERR_RESERVED_4",
			"ERR_RESERVED_5",
			"ERR_RESERVED_6",
			"ERR_PACKET_CRC",
			"ERR_PACKET_MISSED",
			"ERR_PACKET_SIZE",
			"ERR_RESERVED_7",
			"ERR_RESERVED_8",
			"ERR_RESERVED_9",
			"ERR_RESERVED_10",
			"ERR_RESERVED_11",
			"ERR_RESERVED_12",
			"ERR_EDID_CORRUPTED",
			"ERR_NON_LISTED",
			"ERR_SAME_CRC",
			"ERR_WRONG_MODE",
			"ERR_NOT_CONFIGURED"
		};

		/// <summary>
		/// Instantiates a new instance of <see cref="KramerAvSwitchTcp"/>.  Call Initialize() to begin control.
		/// </summary>
		public KramerAvSwitchTcp()
		{
			this.supportedResponses = new Dictionary<string, Action<string>>
			{
				{ ID_RX, this.ParseIdResponse },
				{ ROUTE_RX, this.ParseRouteResponse },
				{ FREEZE_RX, this.ParseFreezeResponse },
				{ VMUTE_RX, this.ParseBlankResponse },
				{ ERR_RX, this.ParseErrorResponse },
				{ AMUTE_RX, this.ParseAudioMuteRx },
				{ ALEVEL_RX, this.ParseAudioLevelRx },
				{ MICLEVEL_RX, this.ParseMicLevelRx }
			};
			this.avOutputs = new List<KramerAvChannel>();
			this.avInputs = new List<KramerAvChannel>();
			this.mics = new List<KramerAvChannel>();
			this.cmdQueue = new Queue<string>();
		}

		~KramerAvSwitchTcp()
		{
			this.Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, uint>> VideoRouteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> VideoBlankChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> VideoFreezeChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioOutputMuteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioOutputLevelChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioInputMuteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioInputLevelChanged;

		/// <inheritdoc/>
		public bool FreezeState
		{
			get
			{
				var found = this.avOutputs.Find(x => x.Number == 1);
				if (found == default(KramerAvChannel))
				{
					Logger.Warn("KramerAvSwitchTcp.FreezeState - could not find an output channel.");
					return false;
				}

				return found.VideoFreeze; ;
			}
		}

		/// <inheritdoc/>
		public bool BlankState
		{
			get
			{
				var found = this.avOutputs.Find(x => x.Number == 1);
				if (found == default(KramerAvChannel))
				{
					Logger.Warn("KramerAvSwitchTcp.BlankState - could not find an output channel.");
					return false;
				}

				return found.VideoMute;
			}
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioPresetIds()
		{
			return new List<string>();
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioInputIds()
		{
			return this.mics.Select(x => x.Id).ToList();
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioOutputIds()
		{
			return this.avOutputs.Select(x => x.Id).ToList();
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc/>
		public void Initialize(string hostName, int port, string id, string label, int numInputs, int numOutputs)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "Ctor", "id");
			ParameterValidator.ThrowIfNullOrEmpty(label, "Ctor", "label");
			ParameterValidator.ThrowIfNullOrEmpty(hostName, "Ctor", "hostName");

			this.Id = id;
			this.Label = label;
			this.numInputs = numInputs;
			this.numOutputs = numOutputs;
			this.hostname = hostName;
			this.port = port;

			if (client != null)
			{
				this.UnsubscribeClient();
				this.client.Dispose();
			}

			this.client = new BasicTcpClient(hostName, port, 1024)
			{
				EnableReconnect = true
			};
			this.SubscribeClient();
		}

		/// <inheritdoc/>
		public override void Connect()
		{
			if (this.client != null && !this.IsOnline)
			{
				Logger.Debug("KramverAvSwitchTcp {0} - Connect()", this.Id);
				this.client.Connect();
			}
		}

		/// <inheritdoc/>
		public override void Disconnect()
		{
			if (this.client != null && this.IsOnline)
			{
				Logger.Debug("KramverAvSwitchTcp {0} - Disconnect()", this.Id);
				this.client.Disconnect();
			}
		}

		/// <inheritdoc/>
		public uint GetCurrentVideoSource(uint output)
		{
			var found = this.avOutputs.Find(x => x.Number == output);
			if (found == default(KramerAvChannel))
			{
				Logger.Error("KramerAvSwitchTcp.GetCurrentVideoSource({0}) - cannot find channel.", output);
				return 0;
			}

			return found.CurrentSource;
		}

		/// <inheritdoc/>
		public void RouteVideo(uint source, uint output)
		{
			if (this.IsOnline)
			{
				this.cmdQueue.Enqueue(string.Format("#ROUTE 1,{0},{1}\r\n", output, source));
				this.TrySend();
			}
		}

		/// <inheritdoc/>
		public void ClearVideoRoute(uint output) { }

		/// <inheritdoc/>
		public void VideoBlankOn()
		{
			if (this.IsOnline)
			{
				this.cmdQueue.Enqueue(string.Format("#VMUTE {0},1\r\n", VIDEO_OUTPUT_NUMBER));
				this.TrySend();
			}
		}

		/// <inheritdoc/>
		public void VideoBlankOff()
		{
			if (this.IsOnline)
			{
				this.cmdQueue.Enqueue(string.Format("#VMUTE {0},0\r\n", VIDEO_OUTPUT_NUMBER));
				this.TrySend();
			}
		}

		/// <inheritdoc/>
		public void FreezeOn()
		{
			if (this.IsOnline)
			{
				this.cmdQueue.Enqueue(string.Format("#VFRZ {0},1\r\n", VIDEO_OUTPUT_NUMBER));
				this.TrySend();
			}
		}

		/// <inheritdoc/>
		public void FreezeOff()
		{
			if (this.IsOnline)
			{
				this.cmdQueue.Enqueue(string.Format("#VFRZ {0},0\r\n", VIDEO_OUTPUT_NUMBER));
				this.TrySend();
			}
		}

		/// <summary>
		/// Interface feature not suported.
		/// </summary>
		/// <param name="id">unused</param>
		public void RecallAudioPreset(string id) { }

		/// <inheritdoc/>
		public void AddInputChannel(string id, string levelTag, string muteTag, int bankIndex, int levelMax, int levelMin, int routerIndex)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "AddInputChannel", "id");

			// Check to see if there already is an input channel with the given index. If so, change the ID to match
			// what is expected by the using application.
			var existing = this.mics.Find(x => x.Number == bankIndex);
			if (existing == default(KramerAvChannel))
			{
				Logger.Debug("KramerAvSwitchTcp at ID {0}: adding new channel: {1}", this.Id, id);
				this.mics.Add(new KramerAvChannel()
				{
					Id = id,
					Number = (uint)bankIndex,
					VideoFreeze = false,
					VideoMute = false,
					AudioLevel = 0,
					AudioMute = false,
					UnmutedLevel = 0,
					CurrentSource = 0
				});
			}
			else
			{
				existing.Id = id;
			}
		}

		/// <inheritdoc/>
		public void AddOutputChannel(string id, string levelTag, string muteTag, string routerTag, int bankIndex, int levelMax, int levelMin, int routerIndex)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "AddOutputChannel", "id");

			// Check to see if there already is an output channel with the given index. If so, change the ID to match
			// what is expected by the using application.
			var existing = this.avOutputs.FirstOrDefault(x => x.Number == bankIndex);
			if (existing == default(KramerAvChannel))
			{
				this.avOutputs.Add(new KramerAvChannel()
				{
					Id = id,
					Number = (uint)bankIndex,
					VideoFreeze = false,
					VideoMute = false,
					AudioLevel = 0,
					AudioMute = false,
					UnmutedLevel = 0,
					CurrentSource = 0
				});
			}
			else
			{
				existing.Id = id;
			}
		}

		/// <summary>
		/// Interface method not supported by this device.
		/// </summary>
		public void AddPreset(string id, int index)
		{
			Logger.Warn(
				"KramerAvSwitchTcp {0} AddPreset({1},{2}) - Presets not supported by this device.",
				this.Id,
				id,
				index);
		}

		/// <inheritdoc/>
		public void SetAudioOutputLevel(string id, int level)
		{
			if (this.IsOnline)
			{
				var found = this.FindOrCreateOutput(VIDEO_OUTPUT_NUMBER);
				int newVal = level;
				if (newVal < 0)
				{
					newVal = 0;
				}
				else if (newVal > 100)
				{
					newVal = 100;
				}

				string cmd = string.Format("#AUD-LVL {0},{1},{2}\r\n", 1, found.Number - 1, newVal);
				this.cmdQueue.Enqueue(cmd);
				this.TrySend();
			}
		}

		/// <inheritdoc/>
		public int GetAudioOutputLevel(string id)
		{
			var found = this.avOutputs.Find(x => x.Number == 1);
			if (found == default(KramerAvChannel))
			{
				Logger.Warn("KramerAvSwitchTcp.AudioOutputLevel - could not find an output channel.");
				return 0;
			}

			return found.AudioLevel;
		}

		/// <inheritdoc/>
		public bool GetAudioOutputMute(string id)
		{
			var found = this.avOutputs.Find(x => x.Id == id);
			if (found == default(KramerAvChannel))
			{
				Logger.Warn("KramerAvSwitchTcp.AudioOutputMute - could not find an output channel.");
				return false;
			}

			return found.AudioMute;
		}

		/// <inheritdoc/>
		public void SetAudioOutputMute(string id, bool state)
		{
			if (this.IsOnline)
			{
				var found = this.avOutputs.FirstOrDefault(x => x.Id == id);
				if (found == default(KramerAvChannel))
				{
					Logger.Warn("KramerAvSwitchTcp.SetAudioOutputMute() - Unable to find an audio output.");
					return;
				}

				int newState = state ? 1 : 0;
				string cmd = string.Format("#MUTE {0},{1}\r\n", found.Number, newState);
				this.cmdQueue.Enqueue(cmd);
				this.TrySend();
			}
		}

		/// <inheritdoc/>
		public void SetAudioInputLevel(string id, int level)
		{
			if (this.IsOnline)
			{
				var found = this.mics.FirstOrDefault(x => x.Id == id);
				Logger.Debug("KramerAvSwitchTcp at ID {0}: found channel with ID {1} and bank {2}", this.Id, found.Id, found.Number);

				int newVal = level;
				if (newVal < 0)
				{
					newVal = 0;
				}
				else if (newVal > 100)
				{
					newVal = 100;
				}

				string cmd = string.Format("#MIC-GAIN {0},{1}\r\n", found.Number, newVal);
				this.cmdQueue.Enqueue(cmd);
				this.TrySend();
			}
		}

		/// <inheritdoc/>
		public void SetAudioInputMute(string id, bool state)
		{
			if (this.IsOnline)
			{
				var found = this.mics.FirstOrDefault(x => x.Id == id);
				if (found == default(KramerAvChannel))
				{
					Logger.Warn("KramerAvSwitchTcp.SetAudioInputMute() - Unable to find an audio input.");
					return;
				}

				if (found.AudioMute == state)
				{
					return;
				}

				// The Kramer P3000 doesn't have an explicit mic mute so we have to set the level to 0 and save the
				// current level for when the channel is unmuted.
				if (state)
				{
					found.UnmutedLevel = found.AudioLevel;
					this.cmdQueue.Enqueue(string.Format("#MIC-GAIN {0},0\r\n", found.Number));
				}
				else
				{
					this.cmdQueue.Enqueue(string.Format("#MIC-GAIN {0},{1}\r\n", found.Number, found.UnmutedLevel));
				}

				this.TrySend();
			}
		}

		/// <inheritdoc/>
		public int GetAudioInputLevel(string id)
		{
			var found = this.mics.Find(x => x.Id == id);
			if (found == default(KramerAvChannel))
			{
				Logger.Warn("KramerAvSwitchTcp.AudioInputLevel - Could not find a mic channel.");
				return 0;
			}

			return found.AudioLevel;
		}

		/// <inheritdoc/>
		public bool GetAudioInputMute(string id)
		{
			var found = this.mics.Find(x => x.Id == id);
			if (found == default(KramerAvChannel))
			{
				Logger.Warn("KramerAvSwitchTcp.AudioInputLevel - Could not find a mic channel.");
				return false;
			}

			return found.AudioMute;
		}

		private void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				if (disposing)
				{
					if (this.client != null)
					{
						this.UnsubscribeClient();
						this.client.Dispose();
						this.client = null;
					}

					this.rxTimer?.Dispose();
				}

				this.disposed = true;
			}
		}

		private void QueryState()
		{
			var found = this.FindOrCreateOutput(VIDEO_OUTPUT_NUMBER);
			this.cmdQueue.Enqueue(string.Format("#ROUTE? 1,{0}\r\n", found.Number));
			this.cmdQueue.Enqueue(string.Format("#VMUTE? {0}\r\n", found.Number));
			this.cmdQueue.Enqueue(string.Format("#VFRZ? {0}\r\n", found.Number));
			this.cmdQueue.Enqueue(string.Format("#MUTE? {0}\r\n", found.Number));
			this.cmdQueue.Enqueue(string.Format("#AUD-LVL? 1,{0}\r\n", found.Number - 1));

			found = this.FindOrCreateMic(MIC_NUMBER);
			this.cmdQueue.Enqueue(string.Format("#MIC-GAIN? {0}\r\n", found.Number));
			this.TrySend();
		}

		private void RxTimeoutHandler(object callbackObject)
		{
			if (this.cmdQueue.Count > 0)
			{
				string command = this.cmdQueue.Dequeue();
				Logger.Error("KramerAvSwitchTcp {0} - no response to command {1}.", this.Id, command);
			}

			this.cmdQueue.Clear();
			this.client.Disconnect();

			this.UnsubscribeClient();
			this.client.Dispose();
			this.client = new BasicTcpClient(hostname, port, bufferSize)
			{
				EnableReconnect = true
			};
			this.SubscribeClient();
			this.client.Connect();
		}

		private void PollTimerHandler(object callbackObject)
		{
			if (!sending)
			{
				var found = this.FindOrCreateInput(VIDEO_OUTPUT_NUMBER);
				cmdQueue.Enqueue(string.Format("#ROUTE? 1,{0}\r\n", found.Number));
				TrySend();
			}

			if (pollTimer != null && !disposed)
			{
				pollTimer.Reset(POLL_TIME);
			}
		}

		private void TrySend()
		{
			if (this.IsOnline && !sending && this.cmdQueue.Count > 0)
			{
				this.sending = true;
				this.client.Send(this.cmdQueue.Dequeue());

				if (this.rxTimer == null)
				{
					this.rxTimer = new CTimer(this.RxTimeoutHandler, RX_TIMEOUT_LENGTH);
				}
				else
				{
					this.rxTimer.Reset(RX_TIMEOUT_LENGTH);
				}
			}
		}

		private void ClientStringReceived(object sender, GenericSingleEventArgs<string> e)
		{
			Logger.Debug("KramverAvSwitchTcp {0} - ClientStringReceived() - {1}", this.Id, e.Arg);
			this.rxTimer.Stop();
			this.sending = false;
			foreach (var kvp in this.supportedResponses)
			{
				if (Regex.IsMatch(e.Arg, kvp.Key))
				{
					kvp.Value.Invoke(e.Arg);
				}
			}

			this.TrySend();
		}

		private void ClientStatusChangedHandler(object sender, EventArgs e)
		{
			if (!this.client.Connected)
			{
				this.cmdQueue.Clear();
				this.sending = false;
			}

			this.IsOnline = this.client.Connected;
			this.NotifyOnlineStatus();
		}

		private void ClientConnectFailedHandler(object sender, GenericSingleEventArgs<SocketStatus> e)
		{
			this.IsOnline = this.client.Connected;
			this.cmdQueue.Clear();
			this.sending = false;
			this.NotifyOnlineStatus();
		}

		private void ClientConnectedHandler(object sender, EventArgs e)
		{
			this.IsOnline = this.client.Connected;
			this.NotifyOnlineStatus();
			this.QueryState();

			if (pollTimer == null)
			{
				pollTimer = new CTimer(PollTimerHandler, POLL_TIME);
			}
			else
			{
				pollTimer.Reset(POLL_TIME);
			}
		}

		// Internal helpers
		private void ParseIdResponse(string response)
		{
			try
			{
				Match match = Regex.Match(response, ID_RX);
				this.devId = short.Parse(match.Groups["id"].Value);
			}
			catch (Exception e)
			{
				Logger.Error(e.Message, "KramerAvSwitchTcp.ParseIdResponse()");
			}
		}

		private void ParseRouteResponse(string response)
		{
			try
			{
				Match match = Regex.Match(response, ROUTE_RX);
				uint output = uint.Parse(match.Groups["output"].Value);
				uint input = uint.Parse(match.Groups["input"].Value);

				KramerAvChannel channel = this.FindOrCreateOutput(output);
				channel.CurrentSource = input;

				var temp = this.VideoRouteChanged;
				temp?.Invoke(this, new GenericDualEventArgs<string, uint>(this.Id, output));
			}
			catch (Exception ex)
			{
				Logger.Error(ex, "KramerAvSwitchTcp.ParseRouteResponsee()");
			}
		}

		private void ParseFreezeResponse(string response)
		{
			try
			{
				Match match = Regex.Match(response, FREEZE_RX);
				uint output = uint.Parse(match.Groups["output"].Value);
				uint state = uint.Parse(match.Groups["flag"].Value);

				KramerAvChannel channel = this.FindOrCreateOutput(output);
				channel.VideoFreeze = state != 0;

				var temp = this.VideoFreezeChanged;
				temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));

			}
			catch (Exception e)
			{
				Logger.Error(e, "KramerAvSwitchTcp.ParseFreezeResponse()");
			}

		}

		private void ParseBlankResponse(string response)
		{
			try
			{
				Match match = Regex.Match(response, VMUTE_RX);
				uint output = uint.Parse(match.Groups["output"].Value);
				uint state = uint.Parse(match.Groups["flag"].Value);

				KramerAvChannel channel = this.FindOrCreateOutput(output);
				channel.VideoMute = (state == 1);

				var temp = this.VideoBlankChanged;
				temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
			}
			catch (Exception e)
			{
				Logger.Error(e, "KramerAvSwitchTcp.ParseBlankResponse()");
			}
		}

		private void ParseErrorResponse(string response)
		{
			try
			{
				short errCode = short.Parse(Regex.Match(response, ERR_RX).Groups["err"].Value);
				if (errCode >= 0 && errCode < ERR_CODES.Length)
				{
					Logger.Error("KramerAvSwitchTcp at ID {0}: ERROR response received: {1}", this.Id, ERR_CODES[errCode]);
				}
				else
				{
					Logger.Error("KramerAvSwitchTcp at ID {0}: Unknown error code received: {1}", this.Id, errCode);
				}
			}
			catch (Exception e)
			{
				Logger.Error(e, "KramerAvSwitchTcp.ParseErrorResponse()");
			}
		}

		private void ParseAudioMuteRx(string response)
		{
			try
			{
				Match match = Regex.Match(response, AMUTE_RX);
				uint output = uint.Parse(match.Groups["output"].Value);
				uint state = uint.Parse(match.Groups["state"].Value);

				KramerAvChannel channel = this.FindOrCreateOutput(output);
				channel.AudioMute = (state == 1);

				var temp = AudioOutputMuteChanged;
				temp?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, channel.Id));
			}
			catch (Exception e)
			{
				Logger.Error(e, "KramerAvSwitchTcp.ParseAudioMuteRx()");
				Logger.Error("{0}", e.StackTrace);
			}
		}

		private void ParseAudioLevelRx(string response)
		{
			try
			{
				Match match = Regex.Match(response, ALEVEL_RX);
				uint mode = uint.Parse(match.Groups["mode"].Value);
				uint index = uint.Parse(match.Groups["index"].Value);
				int level = int.Parse(match.Groups["level"].Value);

				if (mode == 1)
				{
					// level change is on an output channel
					KramerAvChannel channel = this.FindOrCreateOutput(index + 1);
					channel.AudioLevel = level;

					var temp = this.AudioOutputLevelChanged;
					temp?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, channel.Id));
				}
				else
				{
					// level change is an input
					KramerAvChannel input = this.FindOrCreateInput(index);
					input.AudioLevel = level;

					var temp = this.AudioInputLevelChanged;
					temp?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, this.Id));
				}
			}
			catch (Exception e)
			{
				Logger.Error(e, "KramerAvSwitchTc.ParseAudioLevelRx()");
			}
		}

		private void ParseMicLevelRx(string response)
		{
			try
			{
				Match match = Regex.Match(response, MICLEVEL_RX);
				uint index = uint.Parse(match.Groups["micId"].Value);
				int level = int.Parse(match.Groups["level"].Value);

				KramerAvChannel mic = this.FindOrCreateMic(index);
				mic.AudioLevel = level;
				var temp = this.AudioInputLevelChanged;
				temp?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, mic.Id));

				bool muted = level < 1;
				if (muted != mic.AudioMute)
				{
					mic.AudioMute = muted;
					temp = this.AudioInputMuteChanged;
					temp?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, mic.Id));
				}
			}
			catch (Exception ex)
			{
				ErrorLog.Exception("KramerAvSwitchTcp.ParseMicLevelRx()", ex);
			}
		}

		private void UnsubscribeClient()
		{
			this.client.ConnectionFailed -= this.ClientConnectFailedHandler;
			this.client.ClientConnected -= this.ClientConnectedHandler;
			this.client.StatusChanged -= this.ClientStatusChangedHandler;
			this.client.RxRecieved -= this.ClientStringReceived;
		}

		private void SubscribeClient()
		{
			this.client.ConnectionFailed += this.ClientConnectFailedHandler;
			this.client.ClientConnected += this.ClientConnectedHandler;
			this.client.StatusChanged += this.ClientStatusChangedHandler;
			this.client.RxRecieved += this.ClientStringReceived;
		}

		private KramerAvChannel FindOrCreateOutput(uint number)
		{
			KramerAvChannel found = this.avOutputs.Find(x => x.Number == number);
			if (found == default(KramerAvChannel))
			{
				KramerAvChannel newChannel = new KramerAvChannel()
				{
					Id = "OUT" + number,
					Number = number,
					VideoFreeze = false,
					VideoMute = false,
					AudioLevel = 0,
					AudioMute = false,
					CurrentSource = 0,
				};

				this.avOutputs.Add(newChannel);
				return newChannel;
			}
			else
			{
				return found;
			}
		}

		private KramerAvChannel FindOrCreateInput(uint number)
		{
			KramerAvChannel found = this.avInputs.Find(x => x.Number == number);
			if (found == default(KramerAvChannel))
			{
				KramerAvChannel newChannel = new KramerAvChannel()
				{
					Id = "INPUT" + number,
					Number = number,
					VideoFreeze = false,
					VideoMute = false,
					AudioLevel = 0,
					AudioMute = false,
					CurrentSource = 0
				};

				this.avInputs.Add(newChannel);
				return newChannel;
			}
			else
			{
				return found;
			}
		}

		private KramerAvChannel FindOrCreateMic(uint number)
		{
			KramerAvChannel found = this.mics.Find(x => x.Number == number);
			if (found == default(KramerAvChannel))
			{
				KramerAvChannel newChannel = new KramerAvChannel()
				{
					Id = "MIC" + number,
					Number = number,
					VideoFreeze = false,
					VideoMute = false,
					AudioLevel = 0,
					AudioMute = false,
					CurrentSource = 0
				};

				this.mics.Add(newChannel);
				return newChannel;
			}
			else
			{
				return found;
			}
		}
	}
}
