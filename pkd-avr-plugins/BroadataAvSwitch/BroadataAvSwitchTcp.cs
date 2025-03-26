namespace BroadataAvSwitch
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
	using System;
	using System.Collections.Generic;
	using System.Text.RegularExpressions;

	/// <summary>
	/// System control class for a Broadata AV switch. Specifically design for a LBC-PSW52.
	/// </summary>
	public class BroadataAvSwitchTcp : BaseDevice, IAvSwitcher, IDisposable, IAudioControl
	{
		private const int RxTimeoutLength = 5000;
		private const int PollTime = 60000;
		private const string SourceRx = @"a source (?<input>\d+)\r\n";
		private readonly Dictionary<BroadataCommandTypes, Action<string>> _rxHandlers;
		private readonly Queue<CommandData> _cmdQueue;
		private readonly BroadataAvChannel _output;
		private readonly BroadataAvChannel _input;
		private int _numInputs;
		private int _numOutputs;
		private bool _sending;
		private bool _disposed;
		private BasicTcpClient? _client;
		private CTimer? _rxTimer;
		private CTimer? _pollTimer;

		/// <summary>
		/// Instantiates a new instance of <see cref="BroadataAvSwitchTcp"/> and sets internal tracking to default states.
		/// </summary>
		public BroadataAvSwitchTcp()
		{
			_cmdQueue = new Queue<CommandData>();
			_output = new BroadataAvChannel
			{
				Id = "DEFAULTID"
			};
			_input = new BroadataAvChannel
			{
				Id = "DEFAULTID"
			};

			_numOutputs = 1;
			_numInputs = 5;

			_rxHandlers = new Dictionary<BroadataCommandTypes, Action<string>>
			{
				{ BroadataCommandTypes.Route, HandleSourceRx },
				{ BroadataCommandTypes.Mute, HandleMuteRx },
				{ BroadataCommandTypes.Volume, HandleVolumeRx },
				{ BroadataCommandTypes.MicVolume, HandleMicRx }
			};

			Manufacturer = "Broadata";
			Model = "Av Switch";
		}

		~BroadataAvSwitchTcp()
		{
			Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, uint>>? VideoRouteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputMuteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputLevelChanged;

		/// <summary>
		/// Not supported.
		/// </summary>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputMuteChanged;

		/// <summary>
		/// Not supported.
		/// </summary>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputLevelChanged;

		/// <summary>
		/// Not supported.
		/// </summary>
		public int AudioInputLevel => 0;

		/// <summary>
		/// Not supported.
		/// </summary>
		public bool AudioInputMute => false;

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

			Logger.Debug("BroadataAvSwitch.Initialize({0},{1},{2},{3},{4},{5}", hostName, port, id, label, numInputs, numOutputs);

			Id = id;
			Label = label;
			_numInputs = numInputs;
			_numOutputs = numOutputs;

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
				_client.Connect();
			}
		}

		/// <inheritdoc/>
		public override void Disconnect()
		{
			if (_client != null && IsOnline)
			{
				_client.Disconnect();
			}
		}

		/// <inheritdoc/>
		public uint GetCurrentVideoSource(uint output)
		{
			return _output.CurrentSource;
		}

		/// <summary>
		/// Sends a routing command for the given input to output.
		/// LBC-PSW52 input indices are offset by 1 compared to the device protocol.
		/// 1 = HDMI 1<br/>
		/// 2 = HDMI 2<br/>
		/// 3 = Display Port<br/>
		/// 4 = PC 1 (vga)<br/>
		/// 5 = PC 2 (vga)<br/>
		/// </summary>
		/// <param name="source">the input source to be routed.</param>
		/// <param name="output">The output that the input will be routed to.</param>
		public void RouteVideo(uint source, uint output)
		{
			Logger.Debug("RouteVideo({0}, {1}", source, output);

			if (source > _numInputs)
			{
				Logger.Error("BroadataAvSwitchTcp.RouteVideo({0},{1}) - source out of bounds.", source, output);
				return;
			}

			if (output > _numOutputs)
			{
				Logger.Error("BroadataAvSwitchTcp.RouteVideo({0},{1}) - source out of bounds.", source, output);
				return;
			}

			if (!IsOnline)
			{
				Logger.Error("BroadataAvSwitchTcp.RouteVideo({0},{1}) - cannot route while offline.", source, output);
				return;
			}

			_output.CurrentSource = source;
			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = $"S SOURCE {source - 1}\r\n",
				CommandType = BroadataCommandTypes.Route
			});

			TrySend();
		}

		/// <inheritdoc/>
		public void ClearVideoRoute(uint output)
		{
			Logger.Warn("BroadataAvSwitchTcp.ClearVideoRoute() - Action is not supported.");
		}

		/// <inheritdoc/>
		public void SetAudioOutputLevel(string id, int level)
		{
			if (!IsOnline)
			{
				Logger.Error("BroadataAvSwitchTcp - cannot change audio while offline.");
				return;
			}

			var newVal = level;
			if (newVal < 0)
			{
				newVal = 0;
			}
			else if (newVal > 100)
			{
				newVal = 100;
			}

			_output.AudioLevel = newVal;
			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = $"S OUT-VOL {newVal}\r\n",
				CommandType = BroadataCommandTypes.Volume
			});

			TrySend();
		}

		/// <inheritdoc/>
		public int GetAudioOutputLevel(string id)
		{
			return _output.AudioLevel;
		}

		/// <inheritdoc/>
		public void SetAudioOutputMute(string id, bool state)
		{
			if (!IsOnline)
			{
				Logger.Error("BroadataAvSwitchTcp - cannot change audio while offline.");
				return;
			}

			_output.AudioMute = state;
			var newState = state ? 1 : 0;
			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = $"S MUTE {newState}\r\n",
				CommandType = BroadataCommandTypes.Mute
			});

			TrySend();
		}

		/// <inheritdoc/>
		public bool GetAudioOutputMute(string id)
		{
			return _output.AudioMute;
		}

		/// <inheritdoc/>
		public void SetAudioInputLevel(string id, int level)
		{
			if (!IsOnline)
			{
				Logger.Error("BroadataAvSwitchTcp - cannot change audio while offline.");
				return;
			}

			var newVal = level;
			if (newVal < 0)
			{
				newVal = 0;
			}
			else if (newVal > 100)
			{
				newVal = 100;
			}
			
			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = $"S MIC-VOL {newVal}\r\n",
				CommandType = BroadataCommandTypes.MicVolume,
			});

			TrySend();
		}

		/// <inheritdoc/>
		public int GetAudioInputLevel(string id)
		{
			return _input.AudioLevel;
		}

		/// <inheritdoc/>
		public void SetAudioInputMute(string id, bool state)
		{
			if (!IsOnline)
			{
				Logger.Error("BroadataAvSwitchTcp - cannot change audio while offline.");
				return;
			}

			int level;
			if (_input.AudioMute)
			{
				_input.PreMuteLevel = _input.AudioLevel;
				_input.AudioLevel = 0;
				level = 0;
			}
			else
			{
				_input.AudioLevel = _input.PreMuteLevel;
				level = _input.PreMuteLevel;
			}

			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = $"S MIC-VOL {level}\r\n",
				CommandType = BroadataCommandTypes.MicVolume,
			});

			TrySend();
		}

		/// <inheritdoc/>
		public bool GetAudioInputMute(string id)
		{
			return _input.AudioMute;
		}

		/// <summary>
		/// Interface action is not supported by this device.
		/// </summary>
		/// <param name="id">unused</param>
		public void RecallAudioPreset(string id) { }

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioPresetIds()
		{
			return new List<string>();
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioInputIds()
		{
			return [_input.Id];
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioOutputIds()
		{
			return [_output.Id];
		}

		/// <inheritdoc/>
		public void AddInputChannel(string id, string levelTag, string muteTag, int bankIndex, int levelMax, int levelMin, int routerIndex, List<string> _)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "AddInputChannel", nameof(id));
			_input.Id = id;
		}

		/// <inheritdoc/>
		public void AddOutputChannel(string id, string levelTag, string muteTag, string routerTag, int bankIndex, int levelMax, int levelMin, int routerIndex, List<string> _)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "AddOutputChannel", nameof(id));
			_output.Id = id;
		}

		/// <summary>
		/// Interface method not supported by this device.
		/// </summary>
		public void AddPreset(string id, int index)
		{
			Logger.Warn(
				"BroadataAvSwitchTcp {0} AddPreset({1},{2}) - Presets not supported by this device.",
				Id,
				id,
				index);
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
				}

				_rxTimer?.Dispose();
			}

			_disposed = true;
		}

		private void TrySend()
		{
			if (!IsOnline || _sending || _cmdQueue.Count <= 0) return;
			_sending = true;
			
			Logger.Debug("BroadataAvSwitch {0} - Sending command: {1}", Id, _cmdQueue.Peek().CommandString);
			
			_client?.Send(_cmdQueue.Peek().CommandString);
			if (_rxTimer == null)
			{
				_rxTimer = new CTimer(RxTimerCallback, RxTimeoutLength);
			}
			else
			{
				_rxTimer.Reset(RxTimeoutLength);
			}
		}

		private void RxTimerCallback(object? obj)
		{
			if (_cmdQueue.Count > 0)
			{
				var cmd = _cmdQueue.Dequeue();
				Logger.Error("BroadataAvSwitcher - no response for command of type {0}", cmd.CommandType);
			}

			_sending = false;
			TrySend();
		}

		private void PollTimerCallback(object? ojb)
		{
			if (!IsOnline)
			{
				return;
			}

			QueryStatus();

			_pollTimer?.Reset(PollTime);
		}

		private void QueryStatus()
		{
			if (_sending) return;
			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = "R SOURCE\r\n",
				CommandType = BroadataCommandTypes.Route
			});

			_cmdQueue.Enqueue(new CommandData()
			{
				CommandType = BroadataCommandTypes.Mute,
				CommandString = "R MUTE\r\n"
			});

			_cmdQueue.Enqueue(new CommandData()
			{
				CommandType = BroadataCommandTypes.Volume,
				CommandString = "R OUT-VOL\r\n"
			});

			_cmdQueue.Enqueue(new CommandData()
			{
				CommandType = BroadataCommandTypes.MicVolume,
				CommandString = "R MIC-VOL\r\n",
			});

			TrySend();
		}

		private void ClientStringReceived(object? sender, GenericSingleEventArgs<string> e)
		{
			_rxTimer?.Stop();

			Logger.Debug("BroadataAvSwitchTcp {0} - Received from client: {1}", Id, e.Arg);

			// remove junk characters and ignore handshake
			string formatted = Regex.Replace(e.Arg, @"[\p{C}-[\t\r\n]]+", string.Empty);
			if (formatted.Contains("=") || formatted.Contains("command") || string.IsNullOrEmpty(formatted))
			{
				return;
			}

			if (_cmdQueue.Count > 0)
			{
				var cmd = _cmdQueue.Dequeue();
				_rxHandlers[cmd.CommandType].Invoke(formatted);
			}

			_sending = false;
			TrySend();
		}

		private void ClientStatusChangedHandler(object? sender, EventArgs e)
		{
			_pollTimer?.Stop();
			_rxTimer?.Stop();
			_cmdQueue.Clear();
			IsOnline = _client?.Connected ?? false;
			NotifyOnlineStatus();
		}

		private void ClientConnectFailedHandler(object? sender, GenericSingleEventArgs<SocketStatus> e)
		{
			_pollTimer?.Stop();
			_rxTimer?.Stop();
			_cmdQueue.Clear();
			IsOnline = _client?.Connected ?? false;
			NotifyOnlineStatus();
		}

		private void ClientConnectedHandler(object? sender, EventArgs e)
		{
			IsOnline = _client?.Connected ?? false;
			NotifyOnlineStatus();
			QueryStatus();
			if (_pollTimer != null)
			{
				_pollTimer.Reset(PollTime);
			}
			else
			{
				_pollTimer = new CTimer(PollTimerCallback, PollTime);
			}
		}

		private void HandleSourceRx(string rx)
		{
			if (rx.Contains("OK", StringComparison.OrdinalIgnoreCase))
			{
				Notify(VideoRouteChanged, 1);
				return;
			}

			if (rx.Contains("SOURCE", StringComparison.CurrentCultureIgnoreCase))
			{
				Match sourceMatch = Regex.Match(rx, SourceRx);
				if (sourceMatch.Success)
				{
					try
					{
						_output.CurrentSource = uint.Parse(sourceMatch.Groups["input"].Value) + 1;
						Notify(VideoRouteChanged, 1);
					}
					catch (Exception)
					{
						Logger.Error("BroadataAvSwitch - failed to parse source rx for {0}.", rx);
					}
				}
				else
				{
					Logger.Error("BroadataAvSwitch - RX match not found for {0}.", rx);
				}
			}
			else if (rx.Contains("NG"))
			{
				HandleErrorRx(BroadataCommandTypes.Route);
			}
			else
			{
				try
				{
					_output.CurrentSource = uint.Parse(rx) + 1;
					Notify(VideoRouteChanged, 1);
				}
				catch (Exception)
				{
					Logger.Error("BroadataAvSwitch.HandleSourceRx() - failed to parse source rx: {0}", rx);
				}
			}
		}

		private void HandleMuteRx(string rx)
		{
			if (rx.Contains("OK"))
			{
				Notify(AudioOutputMuteChanged, _output.Id);
			}
			else if (rx.Contains("NG"))
			{
				HandleErrorRx(BroadataCommandTypes.Mute);
			}
			else
			{
				try
				{
					_output.AudioMute = short.Parse(rx) == 1;
					Notify(AudioOutputMuteChanged, _output.Id);
				}
				catch (Exception e)
				{
					Logger.Error(e, "BroadataAvSwitch.HandleMuteRx()");
				}
			}
		}

		private void HandleVolumeRx(string rx)
		{
			if (rx.Contains("OK"))
			{
				Notify(AudioOutputLevelChanged, _output.Id);
			}
			else if (rx.Contains("NG"))
			{
				HandleErrorRx(BroadataCommandTypes.Volume);
			}
			else
			{
				try
				{
					_output.AudioLevel = int.Parse(rx);
					Notify(AudioOutputLevelChanged, _output.Id);
				}
				catch (Exception e)
				{
					Logger.Error(e, "BroadataAvSwitch.HandleVolumeRx()");
				}
			}
		}

		private void HandleMicRx(string rx)
		{
			if (rx.Contains("OK"))
			{
				_input.AudioMute = _input.AudioLevel <= 0;
				Notify(AudioInputLevelChanged, _input.Id);
				Notify(AudioInputMuteChanged, _input.Id);
			}
			else if (rx.Contains("NG"))
			{
				HandleErrorRx(BroadataCommandTypes.MicVolume);
			}
			else
			{
				try
				{
					_input.AudioLevel = int.Parse(rx);
					_input.AudioMute = _input.AudioLevel <= 0;
					Notify(AudioInputLevelChanged, _input.Id);
					Notify(AudioInputMuteChanged, _input.Id);
				}
				catch (Exception e)
				{
					Logger.Error(e, "BroadataAvSwitchTcp.HandleMicRx()");
				}
			}
		}

		private void HandleErrorRx(BroadataCommandTypes command)
		{
			Logger.Error("BroadataAvSwitch - Error response received for command {0}", command);
		}

		private void SubscribeClient()
		{
			if (_client == null) return;
			_client.ConnectionFailed += ClientConnectFailedHandler;
			_client.ClientConnected += ClientConnectedHandler;
			_client.StatusChanged += ClientStatusChangedHandler;
			_client.RxReceived += ClientStringReceived;
		}

		private void UnsubscribeClient()
		{
			if (_client == null) return;
			_client.ConnectionFailed -= ClientConnectFailedHandler;
			_client.ClientConnected -= ClientConnectedHandler;
			_client.StatusChanged -= ClientStatusChangedHandler;
			_client.RxReceived -= ClientStringReceived;
		}

		private void Notify(EventHandler<GenericDualEventArgs<string, string>>? handler, string arg2)
		{
			handler?.Invoke(this, new GenericDualEventArgs<string, string>(Id, arg2));
		}

		private void Notify(EventHandler<GenericDualEventArgs<string, uint>>? handler, uint arg2)
		{
			handler?.Invoke(this, new GenericDualEventArgs<string, uint>(Id, arg2));
		}
	}
}
