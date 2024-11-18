namespace PanasonicProjector
{
	using Crestron.SimplSharp;
	using Crestron.SimplSharp.CrestronSockets;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_common_utils.NetComs;
	using pkd_hardware_service.DisplayDevices;
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// GCU C# framework for controlling a Panasonic projector via ethernet.
	/// </summary>
	public class PanasonicProjectorTcp : IDisplayDevice, IDisposable
	{
		private static readonly int POLL_TIME = 60000;
		private static readonly int RX_TIMEOUT = 3000;
		private readonly string[] inputCommands;
		private readonly Queue<CommandData> cmdQueue;
		private readonly Dictionary<CommandTypes, Action<string>> rxHandlers;
		private readonly Dictionary<string, EventHandler<GenericSingleEventArgs<string>>> AckResponses;
		private bool disposed;
		private bool pollingEnabled;
		private bool powerState;
		private bool blankState;
		private bool freezeState;
		private uint useTime;
		private uint offlineAttemptCounter;
		private BasicTcpClient client;
		private CTimer pollTimer;
		private CTimer rxTimer;
		private bool isSending;
		private bool enabled;

		/// <summary>
		/// Instantiates a new instance of <see cref="PanasonicProjectorTcp"/>. Call Initialize() to begin control.
		/// </summary>
		public PanasonicProjectorTcp()
		{
			this.cmdQueue = new Queue<CommandData>();
			this.rxHandlers = new Dictionary<CommandTypes, Action<string>>()
			{
				{ CommandTypes.Power, this.HandlePowerRx },
				{ CommandTypes.Blank, this.HandleBlankRx },
				{ CommandTypes.Freeze, this.HandleFreezeRx },
				{ CommandTypes.UseTime, this.HandleUseTimeRx },
				{ CommandTypes.Input, this.HandleInputRx },
			};

			this.AckResponses = new Dictionary<string, EventHandler<GenericSingleEventArgs<string>>>()
			{
				{ "00PON\r", this.PowerChanged },
				{ "00POF\r", this.PowerChanged },
				{ "00OSH:0\r", this.VideoBlankChanged },
				{ "00OSH:1\r", this.VideoBlankChanged },
				{ "00OFZ:0\r", this.VideoFreezeChanged },
				{ "00OFZ:1\r", this.VideoFreezeChanged }
			};

			this.inputCommands = new string[] { "00IIS:HD1\r", "00IIS:PC1\r" };
		}

		~PanasonicProjectorTcp()
		{
			this.Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> ConnectionChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> HoursUsedChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> PowerChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> VideoBlankChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> VideoFreezeChanged;

		/// <inheritdoc/>
		public uint HoursUsed { get { return this.useTime; } }

		/// <inheritdoc/>
		public bool EnableReconnect
		{
			get
			{
				return this.client.EnableReconnect;
			}

			set
			{
				this.client.EnableReconnect = value;
			}
		}

		/// <inheritdoc/>
		public bool PowerState { get { return this.powerState; } }

		/// <inheritdoc/>
		public bool SupportsFreeze
		{
			get { return true; }
		}

		/// <inheritdoc/>
		public string Id { get; set; }

		/// <inheritdoc/>
		public bool IsOnline
		{
			get
			{
				return this.client != null && this.client.Connected;
			}
		}

		/// <inheritdoc/>
		public string Label { get; set; }

		/// <inheritdoc/>
		public bool BlankState { get { return this.blankState; } }

		/// <inheritdoc/>
		public bool FreezeState { get { return this.freezeState; } }

		/// <inheritdoc/>
		public bool IsInitialized { get; private set; }

		/// <inheritdoc/>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc/>
		public void Connect()
		{
			if (this.client != null && !this.client.Connected)
			{
				this.enabled = true;
				this.client.Connect();
			}
		}

		/// <inheritdoc/>
		public void Disconnect()
		{
			this.enabled = false;
			this.client?.Disconnect();

			if (this.rxTimer != null)
			{
				this.rxTimer.Dispose();
				this.rxTimer = null;
				this.isSending = false;
			}

			this.cmdQueue.Clear();
		}

		/// <inheritdoc/>
		public void EnablePolling()
		{
			if (!this.enabled)
			{
				Logger.Error("NecProjectorTcp.EnablePolling() - control not enabled, cannot start polling.");
				return;
			}

			this.pollingEnabled = true;
			if (this.pollTimer == null)
			{
				this.pollTimer = new CTimer(this.SendPollCommand, POLL_TIME);
			}
			else
			{
				this.pollTimer.Reset(POLL_TIME);
			}
		}

		/// <inheritdoc/>
		public void DisablePolling()
		{
			this.pollingEnabled = false;
			if (this.pollTimer != null)
			{
				this.pollTimer.Dispose();
				this.pollTimer = null;
			}
		}

		/// <inheritdoc/>
		public void Initialize(string ipAddress, int port, string label, string id)
		{
			if (client != null)
			{
				Logger.Warn("PanasonicProjectorTcp.Initialize() - connection object already exists.");
				return;
			}

			this.IsInitialized = false;
			if (port <= 0)
			{
				throw new ArgumentException(string.Format(
					"PanasonicProjectorTcp.Initialize() - invalid port number: {0}",
					port));
			}

			this.Id = id;
			this.Label = label;

			if (this.client != null)
			{
				this.client.ClientConnected -= this.ClientConnectedHandler;
				this.client.ConnectionFailed -= this.ClientConnectFailedHandler;
				this.client.StatusChanged -= this.ClientStatusChangedHandler;
				this.client.RxRecieved -= this.ClientStringRecievedHandler;
				this.client.Dispose();
			}

			this.client = new BasicTcpClient(ipAddress, port, 2014);
			this.client.ClientConnected += this.ClientConnectedHandler;
			this.client.ConnectionFailed += this.ClientConnectFailedHandler;
			this.client.StatusChanged += this.ClientStatusChangedHandler;
			this.client.RxRecieved += this.ClientStringRecievedHandler;
			this.client.ReconnectTime = 4500;
			this.IsInitialized = true;
		}

		/// <inheritdoc/>
		public void PowerOff()
		{
			if (this.enabled)
			{
				this.powerState = false;
				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandString = "00POF\r",
					CommandType = CommandTypes.Power
				});

				this.TrySend();
			}
		}

		/// <inheritdoc/>
		public void PowerOn()
		{
			if (this.enabled)
			{
				this.powerState = true;
				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandString = "00PON\r",
					CommandType = CommandTypes.Power
				});

				this.TrySend();
			}
		}

		/// <inheritdoc/>
		public void FreezeOff()
		{
			if (this.enabled && this.powerState)
			{
				this.freezeState = false;
				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandString = "OFZ:0\r",
					CommandType = CommandTypes.Freeze
				});

				this.TrySend();
			}
		}

		/// <inheritdoc/>
		public void FreezeOn()
		{
			if (this.enabled && this.powerState)
			{
				this.freezeState = true;
				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandString = "OFZ:1\r",
					CommandType = CommandTypes.Freeze
				});

				this.TrySend();
			}
		}

		/// <inheritdoc/>
		public void VideoBlankOff()
		{
			if (this.enabled && this.powerState)
			{
				this.blankState = false;
				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandString = "00OSH:0\r",
					CommandType = CommandTypes.Blank
				});

				this.TrySend();
			}
		}

		/// <inheritdoc/>
		public void VideoBlankOn()
		{
			if (this.enabled && this.powerState)
			{
				this.blankState = true;
				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandString = "00OSH:1\r",
					CommandType = CommandTypes.Blank
				});

				this.TrySend();
			}
		}

		private void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				if (disposing)
				{
					if (this.client != null)
					{
						this.client.ClientConnected -= this.ClientConnectedHandler;
						this.client.ConnectionFailed -= this.ClientConnectFailedHandler;
						this.client.StatusChanged -= this.ClientStatusChangedHandler;
						this.client.RxRecieved -= this.ClientStringRecievedHandler;
						this.client.Dispose();
					}

					this.pollTimer?.Dispose();

					this.rxTimer?.Dispose();
				}

				this.disposed = true;
			}
		}

		private void SendPollCommand(object callbackObject)
		{
			if (this.enabled && this.pollingEnabled)
			{
				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandString = "00QPW\r",
					CommandType = CommandTypes.Power
				});

				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandString = "00QSH\r",
					CommandType = CommandTypes.Blank
				});

				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandString = "00QFZ\r",
					CommandType = CommandTypes.Freeze
				});

				this.cmdQueue.Enqueue(new CommandData()
				{
					CommandString = "00Q$L\r",
					CommandType = CommandTypes.UseTime
				});

				this.TrySend();
				if (this.enabled && this.pollingEnabled)
				{
					this.pollTimer.Reset(POLL_TIME);
				}
			}
		}

		private void RxTimeoutHandler(object callbackObject)
		{
			Logger.Error(
				"PanasonicProjector {0} - No response to command {1}. Removing from queue.",
				this.Id,
				cmdQueue.Dequeue().CommandType);

			this.isSending = false;
			this.TrySend();
		}

		private void ClientConnectedHandler(object sender, EventArgs e)
		{
			Logger.Debug("PanasonicProjector.ClientConnectedHandler()");
			if (this.offlineAttemptCounter > 1)
			{
				this.Notify(this.ConnectionChanged);
			}

			this.offlineAttemptCounter = 0;
			TrySend();
		}

		private void ClientConnectFailedHandler(object sender, GenericSingleEventArgs<SocketStatus> e)
		{
			Logger.Debug("PanasonicProjector.ClientConnectFailedHandler() - {0}", e.Arg);

			if (!this.enabled)
			{
				this.isSending = false;
				this.offlineAttemptCounter = 0;
				this.rxTimer.Stop();
				this.Notify(this.ConnectionChanged);
			}
			else
			{
				this.offlineAttemptCounter++;
				this.TrySend();
			}
		}

		private void ClientStatusChangedHandler(object sender, EventArgs e)
		{
			if (!this.IsOnline)
			{
				this.rxTimer?.Dispose();

				if (!this.enabled)
				{
					this.isSending = false;
					this.cmdQueue.Clear();
				}
			}

			this.Notify(this.ConnectionChanged);
		}

		private void TrySend()
		{
			if (this.IsOnline)
			{
				if (!this.isSending && this.cmdQueue.Count > 0)
				{
					this.isSending = true;
					var cmd = this.cmdQueue.Peek();

					Logger.Debug("Panasonic Projector {0} - sending command {1}", this.Id, cmd.CommandString);
					this.client.Send(cmd.CommandString);

					if (this.rxTimer == null)
					{
						this.rxTimer = new CTimer(this.RxTimeoutHandler, RX_TIMEOUT);
					}
					else
					{
						this.rxTimer.Reset(RX_TIMEOUT);
					}
				}
			}
		}

		private void ClientStringRecievedHandler(object sender, GenericSingleEventArgs<string> e)
		{
			if (!e.Arg.Contains("NTCONTROL"))
			{
				this.rxTimer?.Stop();

				var cmd = this.cmdQueue.Dequeue();
				this.rxHandlers[cmd.CommandType].Invoke(e.Arg);
				this.isSending = false;
				this.TrySend();
			}
		}

		private void HandlePowerRx(string rx)
		{
			if (!rx.Contains("PON") && !rx.Contains("POF") && !rx.Contains("ERR"))
			{
				try
				{
					short state = short.Parse(rx);
					this.powerState = state > 0;
				}
				catch (FormatException)
				{
					Logger.Error("PanasonicProjectorTcp.HandlePowerRx() - RX {0} is not parsable.", rx);
				}
				catch (OverflowException)
				{
					Logger.Error("PanasonicProjectorTcp.HandlePowerRx() - RX {0} exceeds maximum parsable value.", rx);
				}
				catch (ArgumentException)
				{
					Logger.Error("PanasonicProjectorTcp.HandlePowerRx() - RX is not a number style.");
				}
			}

			Notify(this.PowerChanged);
		}

		private void HandleBlankRx(string rx)
		{
			Logger.Debug("PanasonicProjector {0} - HandleBlankRx({1})", this.Id, rx);

			if (!rx.Contains("OSH") && !rx.Contains("ERR"))
			{
				try
				{
					this.blankState = short.Parse(rx) > 0;
				}
				catch (FormatException)
				{
					Logger.Error("PanasonicProjectorTcp.HandleBlankRx() - RX {0} is not parsable.", rx);
				}
				catch (OverflowException)
				{
					Logger.Error("PanasonicProjectorTcp.HandleBlankRx() - RX {0} exceeds maximum parsable value.", rx);
				}
				catch (ArgumentException)
				{
					Logger.Error("PanasonicProjectorTcp.HandleBlankRx() - RX is not a number style.");
				}
			}

			this.Notify(this.VideoBlankChanged);
		}

		private void HandleFreezeRx(string rx)
		{
			if (!rx.Contains("OFZ") && !rx.Contains("ERR"))
			{
				try
				{
					this.freezeState = short.Parse(rx) > 0;
				}
				catch (FormatException)
				{
					Logger.Error("PanasonicProjectorTcp.HandleFreezeRx() - RX {0} is not parsable.", rx);
				}
				catch (OverflowException)
				{
					Logger.Error("PanasonicProjectorTcp.HandleFreezeRx() - RX {0} exceeds maximum parsable value.", rx);
				}
				catch (ArgumentException)
				{
					Logger.Error("PanasonicProjectorTcp.HandleFreezeRx() - RX is not a number style.");
				}
			}

			this.Notify(this.VideoFreezeChanged);
		}

		private void HandleUseTimeRx(string rx)
		{
			if (!rx.Contains("ERR"))
			{
				try
				{
					this.useTime = uint.Parse(rx);
				}
				catch (FormatException)
				{
					Logger.Error("PanasonicProjectorTcp.HandleUseTimeRx() - RX {0} is not parsable.", rx);
				}
				catch (OverflowException)
				{
					Logger.Error("PanasonicProjectorTcp.HandleUseTimeRx() - RX {0} exceeds maximum parsable value.", rx);
				}
				catch (ArgumentException)
				{
					Logger.Error("PanasonicProjectorTcp.HandleUseTimeRx() - RX is not a number style.");
				}
			}

			this.Notify(this.HoursUsedChanged);
		}

		private void HandleInputRx(string rx) { }

		private void Notify(EventHandler<GenericSingleEventArgs<string>> handler)
		{
			var temp = handler;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}
	}
}
