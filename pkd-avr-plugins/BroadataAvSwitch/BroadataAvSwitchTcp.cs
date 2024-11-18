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
		private static readonly int RX_TIMEOUT_LENGTH = 5000;
		private static readonly int POLL_TIME = 60000;
		private static readonly string SOURCE_RX = @"a source (?<input>\d+)\r\n";
		private readonly Dictionary<BroadataCommandTypes, Action<string>> rxHandlers;
		private readonly Queue<CommandData> cmdQueue;
		private BroadataAvChannel output;
		private BroadataAvChannel input;
		private BasicTcpClient client;
		private CTimer rxTimer;
		private CTimer pollTimer;
		private bool sending;
		private int numInputs;
		private int numOutputs;
		private bool disposed;

		/// <summary>
		/// Instantiates a new instance of <see cref="BroadataAvSwitchTcp"/> and sets internal tracking to default states.
		/// </summary>
		public BroadataAvSwitchTcp()
		{
			this.cmdQueue = new Queue<CommandData>();
			this.output = new BroadataAvChannel
			{
				Id = "DEFAULTID"
			};
			this.input = new BroadataAvChannel
			{
				Id = "DEFAULTID"
			};

			this.numOutputs = 1;
			this.numInputs = 5;

			this.rxHandlers = new Dictionary<BroadataCommandTypes, Action<string>>
			{
				{ BroadataCommandTypes.Route, this.HandleSourceRx },
				{ BroadataCommandTypes.Mute, this.HandleMuteRx },
				{ BroadataCommandTypes.Volume, this.HandleVolumeRx },
				{ BroadataCommandTypes.MicVolume, this.HandleMicRx }
			};
		}

		~BroadataAvSwitchTcp()
		{
			this.Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, uint>> VideoRouteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioOutputMuteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioOutputLevelChanged;

		/// <summary>
		/// Not supported.
		/// </summary>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioInputMuteChanged;

		/// <summary>
		/// Not supported.
		/// </summary>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioInputLevelChanged;

		/// <summary>
		/// Not supported.
		/// </summary>
		public int AudioInputLevel { get { return 0; } }

		/// <summary>
		/// Not supported.
		/// </summary>
		public bool AudioInputMute { get { return false; } }

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

			Logger.Debug("BroadataAvSwitch.Initialize({0},{1},{2},{3},{4},{5}", hostName, port, id, label, numInputs, numOutputs);

			this.Id = id;
			this.Label = label;
			this.numInputs = numInputs;
			this.numOutputs = numOutputs;

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
				this.client.Connect();
			}
		}

		/// <inheritdoc/>
		public override void Disconnect()
		{
			if (this.client != null && this.IsOnline)
			{
				this.client.Disconnect();
			}
		}

		/// <inheritdoc/>
		public uint GetCurrentVideoSource(uint output)
		{
			return this.output.CurrentSource;
		}

		/// <summary>
		/// Sends a routing command for the given input to output.
		/// LBC-PSW52 input indecies are offset by 1 compared to the device protocol.
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

			if (source > this.numInputs)
			{
				Logger.Error("BroadataAvSwitchTcp.RouteVideo({0},{1}) - source out of bounds.", source, output);
				return;
			}

			if (output > this.numOutputs)
			{
				Logger.Error("BroadataAvSwitchTcp.RouteVideo({0},{1}) - source out of bounds.", source, output);
				return;
			}

			if (!this.IsOnline)
			{
				Logger.Error("BroadataAvSwitchTcp.RouteVideo({0},{1}) - cannot route while offline.", source, output);
				return;
			}

			this.output.CurrentSource = source;
			this.cmdQueue.Enqueue(new CommandData()
			{
				CommandString = string.Format("S SOURCE {0}\r\n", source - 1),
				CommandType = BroadataCommandTypes.Route
			});

			this.TrySend();
		}

		/// <inheritdoc/>
		public void ClearVideoRoute(uint output)
		{
			Logger.Warn("BroadataAvSwitchTcp.ClearVideoRoute() - Action is not supported.");
		}

		/// <inheritdoc/>
		public void SetAudioOutputLevel(string id, int level)
		{
			if (!this.IsOnline)
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

			this.output.AudioLevel = newVal;
			this.cmdQueue.Enqueue(new CommandData()
			{
				CommandString = string.Format("S OUT-VOL {0}\r\n", newVal),
				CommandType = BroadataCommandTypes.Volume
			});

			this.TrySend();
		}

		/// <inheritdoc/>
		public int GetAudioOutputLevel(string id)
		{
			return this.output.AudioLevel;
		}

		/// <inheritdoc/>
		public void SetAudioOutputMute(string id, bool state)
		{
			if (!this.IsOnline)
			{
				Logger.Error("BroadataAvSwitchTcp - cannot change audio while offline.");
				return;
			}

			this.output.AudioMute = state;
			int newState = state ? 1 : 0;
			this.cmdQueue.Enqueue(new CommandData()
			{
				CommandString = string.Format("S MUTE {0}\r\n", newState),
				CommandType = BroadataCommandTypes.Mute
			});

			this.TrySend();
		}

		/// <inheritdoc/>
		public bool GetAudioOutputMute(string id)
		{
			return this.output.AudioMute;
		}

		/// <inheritdoc/>
		public void SetAudioInputLevel(string id, int level)
		{
			if (!this.IsOnline)
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

			this.input.AudioLevel = newVal;
			this.cmdQueue.Enqueue(new CommandData()
			{
				CommandString = string.Format("S MIC-VOL {0}\r\n", newVal),
				CommandType = BroadataCommandTypes.MicVolume,
			});

			this.TrySend();
		}

		/// <inheritdoc/>
		public int GetAudioInputLevel(string id)
		{
			if (this.input == null)
			{
				return 0;
			}

			return this.input.AudioLevel;
		}

		/// <inheritdoc/>
		public void SetAudioInputMute(string id, bool state)
		{
			if (!this.IsOnline)
			{
				Logger.Error("BroadataAvSwitchTcp - cannot change audio while offline.");
				return;
			}

			this.input.AudioMute = state;

			int level;
			if (this.input.AudioMute)
			{
				this.input.PreMuteLevel = this.input.AudioLevel;
				this.input.AudioLevel = 0;
				level = 0;
			}
			else
			{
				this.input.AudioLevel = this.input.PreMuteLevel;
				level = this.input.PreMuteLevel;
			}

			this.cmdQueue.Enqueue(new CommandData()
			{
				CommandString = string.Format("S MIC-VOL {0}\r\n", level),
				CommandType = BroadataCommandTypes.MicVolume,
			});

			this.TrySend();
		}

		/// <inheritdoc/>
		public bool GetAudioInputMute(string id)
		{
			if (this.input == null)
			{
				return false;
			}

			return this.input.AudioMute;
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
			return new List<string>() { this.input.Id };
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioOutputIds()
		{
			return new List<string>() { this.output.Id };
		}

		/// <inheritdoc/>
		public void AddInputChannel(string id, string levelTag, string muteTag, int bankIndex, int levelMax, int levelMin, int routerIndex)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "AddInputChannel", "id");
			if (this.input == null)
			{
				this.input = new BroadataAvChannel()
				{
					Id = id,
					Number = bankIndex,
					VideoFreeze = false,
					VideoMute = false,
					AudioLevel = 0,
					AudioMute = false,
					CurrentSource = 0,
				};
			}
			else
			{
				this.input.Id = id;
			}
		}

		/// <inheritdoc/>
		public void AddOutputChannel(string id, string levelTag, string muteTag, string routerTag, int bankIndex, int levelMax, int levelMin, int routerIndex)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "AddOutputChannel", "id");
			if (this.output == null)
			{
				this.output = new BroadataAvChannel()
				{
					Id = id,
					Number = bankIndex,
					VideoFreeze = false,
					VideoMute = false,
					AudioLevel = 0,
					AudioMute = false,
					CurrentSource = 0,
				};
			}
			else
			{
				this.output.Id = id;
			}
		}

		/// <summary>
		/// Interface method not supported by this device.
		/// </summary>
		public void AddPreset(string id, int index)
		{
			Logger.Warn(
				"BroadataAvSwitchTcp {0} AddPreset({1},{2}) - Presets not supported by this device.",
				this.Id,
				id,
				index);
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
					}

					this.rxTimer?.Dispose();
				}

				this.disposed = true;
			}
		}

		private void TrySend()
		{
			if (this.IsOnline && !this.sending && this.cmdQueue.Count > 0)
			{
				this.sending = true;
				Logger.Debug("BroadataAvSwitch {0} - Sending command: {1}", this.Id, this.cmdQueue.Peek().CommandString);
				this.client.Send(this.cmdQueue.Peek().CommandString);
				if (this.rxTimer == null)
				{
					this.rxTimer = new CTimer(this.RxTimerCallback, RX_TIMEOUT_LENGTH);
				}
				else
				{
					this.rxTimer.Reset(RX_TIMEOUT_LENGTH);
				}
			}
		}

		private void RxTimerCallback(object obj)
		{
			if (this.cmdQueue.Count > 0)
			{
				var cmd = this.cmdQueue.Dequeue();
				Logger.Error("BraodataAvSwitcher - no response for command of type {0}", cmd.CommandType);
			}

			this.sending = false;
			this.TrySend();
		}

		private void PollTimerCallback(object ojb)
		{
			if (!this.IsOnline)
			{
				return;
			}

			this.QueryStatus();

			this.pollTimer?.Reset(POLL_TIME);
		}

		private void QueryStatus()
		{
			if (!this.sending)
			{
				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandString = "R SOURCE\r\n",
					CommandType = BroadataCommandTypes.Route
				});

				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandType = BroadataCommandTypes.Mute,
					CommandString = "R MUTE\r\n"
				});

				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandType = BroadataCommandTypes.Volume,
					CommandString = "R OUT-VOL\r\n"
				});

				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandType = BroadataCommandTypes.MicVolume,
					CommandString = "R MIC-VOL\r\n",
				});

				this.TrySend();
			}
		}

		private void ClientStringReceived(object sender, GenericSingleEventArgs<string> e)
		{
			this.rxTimer?.Stop();

			Logger.Debug("BroadataAvSwitchTcp {0} - Received from client: {1}", this.Id, e.Arg);

			// remove junk characters and ignore handshake
			string formatted = Regex.Replace(e.Arg, @"[\p{C}-[\t\r\n]]+", string.Empty);
			if (formatted.Contains("=") || formatted.Contains("command") || string.IsNullOrEmpty(formatted))
			{
				return;
			}

			if (this.cmdQueue.Count > 0)
			{
				var cmd = this.cmdQueue.Dequeue();
				this.rxHandlers[cmd.CommandType].Invoke(formatted);
			}

			this.sending = false;
			this.TrySend();
		}

		private void ClientStatusChangedHandler(object sender, EventArgs e)
		{
			this.pollTimer?.Stop();
			this.rxTimer?.Stop();
			this.cmdQueue.Clear();
			this.IsOnline = this.client.Connected;
			this.NotifyOnlineStatus();
		}

		private void ClientConnectFailedHandler(object sender, GenericSingleEventArgs<SocketStatus> e)
		{
			this.pollTimer?.Stop();
			this.rxTimer?.Stop();
			this.cmdQueue.Clear();
			this.IsOnline = this.client.Connected;
			this.NotifyOnlineStatus();
		}

		private void ClientConnectedHandler(object sender, EventArgs e)
		{
			this.IsOnline = this.client.Connected;
			this.NotifyOnlineStatus();
			this.QueryStatus();
			if (this.pollTimer != null)
			{
				this.pollTimer.Reset(POLL_TIME);
			}
			else
			{
				this.pollTimer = new CTimer(this.PollTimerCallback, POLL_TIME);
			}
		}

		private void HandleSourceRx(string rx)
		{
			if (rx.Contains("OK"))
			{
				this.Notify(this.VideoRouteChanged, 1);
				return;
			}

			if (rx.ToUpper().Contains("SOURCE"))
			{
				Match sourceMatch = Regex.Match(rx, SOURCE_RX);
				if (sourceMatch.Success)
				{
					try
					{
						this.output.CurrentSource = uint.Parse(sourceMatch.Groups["input"].Value) + 1;
						this.Notify(this.VideoRouteChanged, 1);
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
				this.HandleErrorRx(BroadataCommandTypes.Route);
			}
			else
			{
				try
				{
					this.output.CurrentSource = uint.Parse(rx) + 1;
					this.Notify(this.VideoRouteChanged, 1);
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
				this.Notify(this.AudioOutputMuteChanged, this.output.Id);
			}
			else if (rx.Contains("NG"))
			{
				this.HandleErrorRx(BroadataCommandTypes.Mute);
			}
			else
			{
				try
				{
					this.output.AudioMute = short.Parse(rx) == 1;
					this.Notify(this.AudioOutputMuteChanged, this.output.Id);
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
				this.Notify(this.AudioOutputLevelChanged, this.output.Id);
			}
			else if (rx.Contains("NG"))
			{
				this.HandleErrorRx(BroadataCommandTypes.Volume);
			}
			else
			{
				try
				{
					this.output.AudioLevel = int.Parse(rx);
					this.Notify(this.AudioOutputLevelChanged, this.output.Id);
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
				this.input.AudioMute = this.input.AudioLevel <= 0;
				this.Notify(this.AudioInputLevelChanged, this.input.Id);
				this.Notify(this.AudioInputMuteChanged, this.input.Id);
			}
			else if (rx.Contains("NG"))
			{
				this.HandleErrorRx(BroadataCommandTypes.MicVolume);
			}
			else
			{
				try
				{
					this.input.AudioLevel = int.Parse(rx);
					this.input.AudioMute = this.input.AudioLevel <= 0;
					this.Notify(this.AudioInputLevelChanged, this.input.Id);
					this.Notify(this.AudioInputMuteChanged, this.input.Id);
				}
				catch (Exception e)
				{
					Logger.Error(e, "BroadataAvSwitchTcp.HandleMicRx()");
				}
			}
		}

		private void HandleErrorRx(BroadataCommandTypes command)
		{
			Logger.Error("BroadataAvSwitch - Error response rececived for command {0}", command);
		}

		private void SubscribeClient()
		{
			this.client.ConnectionFailed += this.ClientConnectFailedHandler;
			this.client.ClientConnected += this.ClientConnectedHandler;
			this.client.StatusChanged += this.ClientStatusChangedHandler;
			this.client.RxRecieved += this.ClientStringReceived;
		}

		private void UnsubscribeClient()
		{
			this.client.ConnectionFailed -= this.ClientConnectFailedHandler;
			this.client.ClientConnected -= this.ClientConnectedHandler;
			this.client.StatusChanged -= this.ClientStatusChangedHandler;
			this.client.RxRecieved -= this.ClientStringReceived;
		}

		private void Notify(EventHandler<GenericDualEventArgs<string, string>> handler, string arg2)
		{
			handler?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, arg2));
		}

		private void Notify(EventHandler<GenericDualEventArgs<string, uint>> handler, uint arg2)
		{
			handler?.Invoke(this, new GenericDualEventArgs<string, uint>(this.Id, arg2));
		}
	}
}
