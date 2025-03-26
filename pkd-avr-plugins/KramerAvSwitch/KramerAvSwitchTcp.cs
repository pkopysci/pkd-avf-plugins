// warning disabled due to public API.
// ReSharper disable UnusedAutoPropertyAccessor.Global
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
		private const int RxTimeoutLength = 3000;
		private const int PollTime = 15000;
		private const uint VideoOutputNumber = 1;
		private const uint MicNumber = 0;
		private const string IdRx = @"~(?<id>\d{2})@ OK\r\n";
		private const string RouteRx = @"~(?<id>\d{2})@ROUTE (?<layer>\d+),(?<output>\d+),(?<input>\d)+";
		private const string FreezeRx = @"~(?<id>\d{2})@VFRZ (?<output>\d+),(?<flag>\d)";
		private const string VmuteRx = @"~(?<id>\d{2})@VMUTE (?<output>\d+),(?<flag>\d)";
		private const string AmuteRx = @"~(?<id>\d{2})@MUTE (?<output>\d+),(?<state>\d)";
		private const string AlevelRx = @"~(?<id>\d{2})@AUD-LVL (?<mode>\d+),(?<index>\d+),(?<level>\d+)";
		private const string MiclevelRx = @"~(?<id>\d{2})@MIC-GAIN (?<micId>\d+),(?<level>\d+)";
		private const string ErrRx = @"~(?<id>\d{2})@ERR (?<err>\d+)";
		private const int BufferSize = 1024;
		private readonly Dictionary<string, Action<string>> _supportedResponses;
		private readonly Queue<string> _cmdQueue;
		private readonly List<KramerAvChannel> _avOutputs;
		private readonly List<KramerAvChannel> _avInputs;
		private readonly List<KramerAvChannel> _mics;
		private BasicTcpClient? _client;
		private CTimer? _rxTimer;
		private CTimer? _pollTimer;
		private bool _disposed;
		private bool _sending;
		private string _hostname;
		private int _port;

		private static readonly string[] ErrCodes =
		[
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
		];

		/// <summary>
		/// Instantiates a new instance of <see cref="KramerAvSwitchTcp"/>.  Call Initialize() to begin control.
		/// </summary>
		public KramerAvSwitchTcp()
		{
			_supportedResponses = new Dictionary<string, Action<string>>
			{
				{ IdRx, ParseIdResponse },
				{ RouteRx, ParseRouteResponse },
				{ FreezeRx, ParseFreezeResponse },
				{ VmuteRx, ParseBlankResponse },
				{ ErrRx, ParseErrorResponse },
				{ AmuteRx, ParseAudioMuteRx },
				{ AlevelRx, ParseAudioLevelRx },
				{ MiclevelRx, ParseMicLevelRx }
			};
			_avOutputs = [];
			_avInputs = [];
			_mics = [];
			_cmdQueue = new Queue<string>();
			_hostname = string.Empty;

			Manufacturer = "Kramer";
			Model = "VP-440X";
		}

		~KramerAvSwitchTcp()
		{
			Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, uint>>? VideoRouteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>>? VideoBlankChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>>? VideoFreezeChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputMuteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputLevelChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputMuteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputLevelChanged;

		/// <inheritdoc/>
		public bool FreezeState
		{
			get
			{
				var found = _avOutputs.Find(x => x.Number == 1);
				if (found == null)
				{
					Logger.Warn("KramerAvSwitchTcp.FreezeState - could not find an output channel.");
					return false;
				}

				return found.VideoFreeze;
			}
		}

		/// <inheritdoc/>
		public bool BlankState
		{
			get
			{
				var found = _avOutputs.Find(x => x.Number == 1);
				if (found == null)
				{
					Logger.Warn("KramerAvSwitchTcp.BlankState - could not find an output channel.");
					return false;
				}

				return found.VideoMute;
			}
		}

		/// <summary>
		/// Gets the ID of the Kramer device as it is defined on the hardware as of the last query response.
		/// </summary>
		public short DeviceId { get; private set; }

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioPresetIds()
		{
			return new List<string>();
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioInputIds()
		{
			return _mics.Select(x => x.Id).ToList();
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioOutputIds()
		{
			return _avOutputs.Select(x => x.Id).ToList();
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc/>
		public void Initialize(string hostName, int port, string id, string label, int numInputs, int numOutputs)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "Ctor", nameof(id));
			ParameterValidator.ThrowIfNullOrEmpty(label, "Ctor", nameof(label));
			ParameterValidator.ThrowIfNullOrEmpty(hostName, "Ctor", nameof(hostName));

			Id = id;
			Label = label;
			_hostname = hostName;
			_port = port;

			if (_client != null)
			{
				UnsubscribeClient();
				_client.Dispose();
			}

			_client = new BasicTcpClient(hostName, port, 1024)
			{
				EnableReconnect = true
			};
			SubscribeClient();
		}

		/// <inheritdoc/>
		public override void Connect()
		{
			if (_client != null && !IsOnline)
			{
				Logger.Debug("KramerAvSwitchTcp {0} - Connect()", Id);
				_client.Connect();
			}
		}

		/// <inheritdoc/>
		public override void Disconnect()
		{
			if (_client != null && IsOnline)
			{
				Logger.Debug("KramerAvSwitchTcp {0} - Disconnect()", Id);
				_client.Disconnect();
			}
		}

		/// <inheritdoc/>
		public uint GetCurrentVideoSource(uint output)
		{
			var found = _avOutputs.Find(x => x.Number == output);
			if (found == null)
			{
				Logger.Error("KramerAvSwitchTcp.GetCurrentVideoSource({0}) - cannot find channel.", output);
				return 0;
			}

			return found.CurrentSource;
		}

		/// <inheritdoc/>
		public void RouteVideo(uint source, uint output)
		{
			if (IsOnline)
			{
				_cmdQueue.Enqueue($"#ROUTE 1,{output},{source}\r\n");
				TrySend();
			}
		}

		/// <inheritdoc/>
		public void ClearVideoRoute(uint output) { }

		/// <inheritdoc/>
		public void VideoBlankOn()
		{
			if (IsOnline)
			{
				_cmdQueue.Enqueue($"#VMUTE {VideoOutputNumber},1\r\n");
				TrySend();
			}
		}

		/// <inheritdoc/>
		public void VideoBlankOff()
		{
			if (IsOnline)
			{
				_cmdQueue.Enqueue($"#VMUTE {VideoOutputNumber},0\r\n");
				TrySend();
			}
		}

		/// <inheritdoc/>
		public void FreezeOn()
		{
			if (IsOnline)
			{
				_cmdQueue.Enqueue($"#VFRZ {VideoOutputNumber},1\r\n");
				TrySend();
			}
		}

		/// <inheritdoc/>
		public void FreezeOff()
		{
			if (IsOnline)
			{
				_cmdQueue.Enqueue($"#VFRZ {VideoOutputNumber},0\r\n");
				TrySend();
			}
		}

		/// <summary>
		/// Interface feature not suported.
		/// </summary>
		/// <param name="id">unused</param>
		public void RecallAudioPreset(string id) { }

		/// <inheritdoc/>
		public void AddInputChannel(string id, string levelTag, string muteTag, int bankIndex, int levelMax, int levelMin, int routerIndex, List<string> _)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "AddInputChannel", "id");

			// Check to see if there already is an input channel with the given index. If so, change the ID to match
			// what is expected by the using application.
			var existing = _mics.Find(x => x.Number == bankIndex);
			if (existing == null)
			{
				Logger.Debug("KramerAvSwitchTcp at ID {0}: adding new channel: {1}", Id, id);
				_mics.Add(new KramerAvChannel()
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
		public void AddOutputChannel(string id, string levelTag, string muteTag, string routerTag, int bankIndex, int levelMax, int levelMin, int routerIndex, List<string> _)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "AddOutputChannel", "id");

			// Check to see if there already is an output channel with the given index. If so, change the ID to match
			// what is expected by the using application.
			var existing = _avOutputs.FirstOrDefault(x => x.Number == bankIndex);
			if (existing == null)
			{
				_avOutputs.Add(new KramerAvChannel()
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
				Id,
				id,
				index);
		}

		/// <inheritdoc/>
		public void SetAudioOutputLevel(string id, int level)
		{
			if (IsOnline)
			{
				var found = FindOrCreateOutput(VideoOutputNumber);
				int newVal = level;
				if (newVal < 0)
				{
					newVal = 0;
				}
				else if (newVal > 100)
				{
					newVal = 100;
				}

				string cmd = $"#AUD-LVL {1},{found.Number - 1},{newVal}\r\n";
				_cmdQueue.Enqueue(cmd);
				TrySend();
			}
		}

		/// <inheritdoc/>
		public int GetAudioOutputLevel(string id)
		{
			var found = _avOutputs.Find(x => x.Number == 1);
			if (found == null)
			{
				Logger.Warn("KramerAvSwitchTcp.AudioOutputLevel - could not find an output channel.");
				return 0;
			}

			return found.AudioLevel;
		}

		/// <inheritdoc/>
		public bool GetAudioOutputMute(string id)
		{
			var found = _avOutputs.Find(x => x.Id == id);
			if (found == null)
			{
				Logger.Warn("KramerAvSwitchTcp.AudioOutputMute - could not find an output channel.");
				return false;
			}

			return found.AudioMute;
		}

		/// <inheritdoc/>
		public void SetAudioOutputMute(string id, bool state)
		{
			if (!IsOnline) return;
			var found = _avOutputs.FirstOrDefault(x => x.Id == id);
			if (found == null)
			{
				Logger.Warn("KramerAvSwitchTcp.SetAudioOutputMute() - Unable to find an audio output.");
				return;
			}

			var newState = state ? 1 : 0;
			var cmd = $"#MUTE {found.Number},{newState}\r\n";
			_cmdQueue.Enqueue(cmd);
			TrySend();
		}

		/// <inheritdoc/>
		public void SetAudioInputLevel(string id, int level)
		{
			if (!IsOnline) return;
			var found = _mics.FirstOrDefault(x => x.Id == id);
			if (found == null)
			{
				Logger.Error($"KramerAvSwitchTcp.{nameof(SetAudioInputLevel)}({id}, {level}) - Unable to find audio output.");
				return;
			}
				
			Logger.Debug("KramerAvSwitchTcp at ID {0}: found channel with ID {1} and bank {2}", Id, found.Id, found.Number);

			//int newVal = level;
			var newVal = level switch
			{
				< 0 => 0,
				> 100 => 100,
				_ => level
			};

			var cmd = $"#MIC-GAIN {found.Number},{newVal}\r\n";
			_cmdQueue.Enqueue(cmd);
			TrySend();
		}

		/// <inheritdoc/>
		public void SetAudioInputMute(string id, bool state)
		{
			if (!IsOnline) return;
			var found = _mics.FirstOrDefault(x => x.Id == id);
			if (found == null)
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
				_cmdQueue.Enqueue($"#MIC-GAIN {found.Number},0\r\n");
			}
			else
			{
				_cmdQueue.Enqueue($"#MIC-GAIN {found.Number},{found.UnmutedLevel}\r\n");
			}

			TrySend();
		}

		/// <inheritdoc/>
		public int GetAudioInputLevel(string id)
		{
			var found = _mics.Find(x => x.Id == id);
			if (found == null)
			{
				Logger.Warn("KramerAvSwitchTcp.AudioInputLevel - Could not find a mic channel.");
				return 0;
			}

			return found.AudioLevel;
		}

		/// <inheritdoc/>
		public bool GetAudioInputMute(string id)
		{
			var found = _mics.Find(x => x.Id == id);
			if (found == null)
			{
				Logger.Warn("KramerAvSwitchTcp.AudioInputLevel - Could not find a mic channel.");
				return false;
			}

			return found.AudioMute;
		}

		private void Dispose(bool disposing)
		{
			if (_disposed) return;
			if (disposing)
			{
				if (_client != null)
				{
					UnsubscribeClient();
					_client.Dispose();
					_client = null;
				}

				_rxTimer?.Dispose();
			}

			_disposed = true;
		}

		private void QueryState()
		{
			var found = FindOrCreateOutput(VideoOutputNumber);
			_cmdQueue.Enqueue($"#ROUTE? 1,{found.Number}\r\n");
			_cmdQueue.Enqueue($"#VMUTE? {found.Number}\r\n");
			_cmdQueue.Enqueue($"#VFRZ? {found.Number}\r\n");
			_cmdQueue.Enqueue($"#MUTE? {found.Number}\r\n");
			_cmdQueue.Enqueue($"#AUD-LVL? 1,{found.Number - 1}\r\n");

			found = FindOrCreateMic(MicNumber);
			_cmdQueue.Enqueue($"#MIC-GAIN? {found.Number}\r\n");
			TrySend();
		}

		private void RxTimeoutHandler(object? callbackObject)
		{
			if (_cmdQueue.Count > 0)
			{
				string command = _cmdQueue.Dequeue();
				Logger.Error("KramerAvSwitchTcp {0} - no response to command {1}.", Id, command);
			}

			_cmdQueue.Clear();
			_client?.Disconnect();

			UnsubscribeClient();
			_client?.Dispose();
			_client = new BasicTcpClient(_hostname, _port, BufferSize)
			{
				EnableReconnect = true
			};
			SubscribeClient();
			_client.Connect();
		}

		private void PollTimerHandler(object? callbackObject)
		{
			if (!_sending)
			{
				var found = FindOrCreateInput(VideoOutputNumber);
				_cmdQueue.Enqueue($"#ROUTE? 1,{found.Number}\r\n");
				TrySend();
			}

			if (_pollTimer != null && !_disposed)
			{
				_pollTimer.Reset(PollTime);
			}
		}

		private void TrySend()
		{
			if (!IsOnline || _sending || _cmdQueue.Count <= 0) return;
			_sending = true;
			_client?.Send(_cmdQueue.Dequeue());

			if (_rxTimer == null)
			{
				_rxTimer = new CTimer(RxTimeoutHandler, RxTimeoutLength);
			}
			else
			{
				_rxTimer.Reset(RxTimeoutLength);
			}
		}

		private void ClientStringReceived(object? sender, GenericSingleEventArgs<string> e)
		{
			Logger.Debug("KramerAvSwitchTcp {0} - ClientStringReceived() - {1}", Id, e.Arg);
			_rxTimer?.Stop();
			_sending = false;
			foreach (var kvp in _supportedResponses)
			{
				if (Regex.IsMatch(e.Arg, kvp.Key))
				{
					kvp.Value.Invoke(e.Arg);
				}
			}

			TrySend();
		}

		private void ClientStatusChangedHandler(object? sender, EventArgs e)
		{
			if (_client is not { Connected: true })
			{
				_cmdQueue.Clear();
				_sending = false;
			}

			IsOnline = _client?.Connected ?? false;
			NotifyOnlineStatus();
		}

		private void ClientConnectFailedHandler(object? sender, GenericSingleEventArgs<SocketStatus> e)
		{
			IsOnline = _client?.Connected ?? false;
			_cmdQueue.Clear();
			_sending = false;
			NotifyOnlineStatus();
		}

		private void ClientConnectedHandler(object? sender, EventArgs e)
		{
			IsOnline = _client?.Connected ?? false;
			NotifyOnlineStatus();
			QueryState();

			if (_pollTimer == null)
			{
				_pollTimer = new CTimer(PollTimerHandler, PollTime);
			}
			else
			{
				_pollTimer.Reset(PollTime);
			}
		}

		// Internal helpers
		private void ParseIdResponse(string response)
		{
			try
			{
				Match match = Regex.Match(response, IdRx);
				DeviceId = short.Parse(match.Groups["id"].Value);
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
				Match match = Regex.Match(response, RouteRx);
				uint output = uint.Parse(match.Groups["output"].Value);
				uint input = uint.Parse(match.Groups["input"].Value);

				KramerAvChannel channel = FindOrCreateOutput(output);
				channel.CurrentSource = input;

				var temp = VideoRouteChanged;
				temp?.Invoke(this, new GenericDualEventArgs<string, uint>(Id, output));
			}
			catch (Exception ex)
			{
				Logger.Error(ex, "KramerAvSwitchTcp.ParseRouteResponse()");
			}
		}

		private void ParseFreezeResponse(string response)
		{
			try
			{
				Match match = Regex.Match(response, FreezeRx);
				uint output = uint.Parse(match.Groups["output"].Value);
				uint state = uint.Parse(match.Groups["flag"].Value);

				KramerAvChannel channel = FindOrCreateOutput(output);
				channel.VideoFreeze = state != 0;

				var temp = VideoFreezeChanged;
				temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));

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
				Match match = Regex.Match(response, VmuteRx);
				uint output = uint.Parse(match.Groups["output"].Value);
				uint state = uint.Parse(match.Groups["flag"].Value);

				KramerAvChannel channel = FindOrCreateOutput(output);
				channel.VideoMute = (state == 1);

				var temp = VideoBlankChanged;
				temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
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
				short errCode = short.Parse(Regex.Match(response, ErrRx).Groups["err"].Value);
				if (errCode >= 0 && errCode < ErrCodes.Length)
				{
					Logger.Error("KramerAvSwitchTcp at ID {0}: ERROR response received: {1}", Id, ErrCodes[errCode]);
				}
				else
				{
					Logger.Error("KramerAvSwitchTcp at ID {0}: Unknown error code received: {1}", Id, errCode);
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
				Match match = Regex.Match(response, AmuteRx);
				uint output = uint.Parse(match.Groups["output"].Value);
				uint state = uint.Parse(match.Groups["state"].Value);

				KramerAvChannel channel = FindOrCreateOutput(output);
				channel.AudioMute = (state == 1);

				var temp = AudioOutputMuteChanged;
				temp?.Invoke(this, new GenericDualEventArgs<string, string>(Id, channel.Id));
			}
			catch (Exception e)
			{
				Logger.Error(e, "KramerAvSwitchTcp.ParseAudioMuteRx()");
			}
		}

		private void ParseAudioLevelRx(string response)
		{
			try
			{
				Match match = Regex.Match(response, AlevelRx);
				uint mode = uint.Parse(match.Groups["mode"].Value);
				uint index = uint.Parse(match.Groups["index"].Value);
				int level = int.Parse(match.Groups["level"].Value);

				if (mode == 1)
				{
					// level change is on an output channel
					KramerAvChannel channel = FindOrCreateOutput(index + 1);
					channel.AudioLevel = level;

					var temp = AudioOutputLevelChanged;
					temp?.Invoke(this, new GenericDualEventArgs<string, string>(Id, channel.Id));
				}
				else
				{
					// level change is an input
					KramerAvChannel input = FindOrCreateInput(index);
					input.AudioLevel = level;

					var temp = AudioInputLevelChanged;
					temp?.Invoke(this, new GenericDualEventArgs<string, string>(Id, Id));
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
				Match match = Regex.Match(response, MiclevelRx);
				uint index = uint.Parse(match.Groups["micId"].Value);
				int level = int.Parse(match.Groups["level"].Value);

				KramerAvChannel mic = FindOrCreateMic(index);
				mic.AudioLevel = level;
				var temp = AudioInputLevelChanged;
				temp?.Invoke(this, new GenericDualEventArgs<string, string>(Id, mic.Id));

				bool muted = level < 1;
				if (muted != mic.AudioMute)
				{
					mic.AudioMute = muted;
					temp = AudioInputMuteChanged;
					temp?.Invoke(this, new GenericDualEventArgs<string, string>(Id, mic.Id));
				}
			}
			catch (Exception ex)
			{
				ErrorLog.Exception("KramerAvSwitchTcp.ParseMicLevelRx()", ex);
			}
		}

		private void UnsubscribeClient()
		{
			if (_client == null) return;
			_client.ConnectionFailed -= ClientConnectFailedHandler;
			_client.ClientConnected -= ClientConnectedHandler;
			_client.StatusChanged -= ClientStatusChangedHandler;
			_client.RxReceived -= ClientStringReceived;
		}

		private void SubscribeClient()
		{
			if (_client == null) return;
			_client.ConnectionFailed += ClientConnectFailedHandler;
			_client.ClientConnected += ClientConnectedHandler;
			_client.StatusChanged += ClientStatusChangedHandler;
			_client.RxReceived += ClientStringReceived;
		}

		private KramerAvChannel FindOrCreateOutput(uint number)
		{
			var found = _avOutputs.Find(x => x.Number == number);
			if (found == null)
			{
				var newChannel = new KramerAvChannel()
				{
					Id = "OUT" + number,
					Number = number,
					VideoFreeze = false,
					VideoMute = false,
					AudioLevel = 0,
					AudioMute = false,
					CurrentSource = 0,
				};

				_avOutputs.Add(newChannel);
				return newChannel;
			}
			
			return found;
		}

		private KramerAvChannel FindOrCreateInput(uint number)
		{
			var found = _avInputs.Find(x => x.Number == number);
			if (found == null)
			{
				var newChannel = new KramerAvChannel()
				{
					Id = "INPUT" + number,
					Number = number,
					VideoFreeze = false,
					VideoMute = false,
					AudioLevel = 0,
					AudioMute = false,
					CurrentSource = 0
				};

				_avInputs.Add(newChannel);
				return newChannel;
			}
			
			return found;
		}

		private KramerAvChannel FindOrCreateMic(uint number)
		{
			var found = _mics.Find(x => x.Number == number);
			if (found == null)
			{
				var newChannel = new KramerAvChannel()
				{
					Id = "MIC" + number,
					Number = number,
					VideoFreeze = false,
					VideoMute = false,
					AudioLevel = 0,
					AudioMute = false,
					CurrentSource = 0
				};

				_mics.Add(newChannel);
				return newChannel;
			}
			
			return found;
		}
	}
}
