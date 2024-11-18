namespace PlanarSimplicityDisplay
{
	using Crestron.SimplSharp;
	using Crestron.SimplSharp.CrestronSockets;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_common_utils.NetComs;
	using pkd_common_utils.Validation;
	using pkd_hardware_service.DisplayDevices;
	using System;
	using System.Collections.Generic;

	public class PlanarSimplicityDisplayTcp : IDisplayDevice, IDisposable
	{
		private static readonly int POLL_TIME = 30000;
		private static readonly int OFFLINE_TIMEOUT = 60000;

		private static readonly byte TX_HEADER = 0xA6;
		private static readonly byte ACK_RX = 0x00;
		private static readonly byte POWER_RX = 0x19;
		private static readonly byte[] POWER_QUERY_CMD = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x03, 0x01, 0x19 };
		private static readonly byte[] POWER_ON_CMD = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x04, 0x01, 0x18, 0x02 };
		private static readonly byte[] POWER_OFF_CMD = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x04, 0x01, 0x18, 0x01 };

		private bool disposed;
		private bool pollingEnabled;
		private bool offlineTimerActive;
		private bool reconnectEnabled;
		private BasicTcpClient client;
		private CTimer pollTimer;
		private CTimer offlineTimer;

		public PlanarSimplicityDisplayTcp() { }

		~PlanarSimplicityDisplayTcp()
		{
			this.Dispose(false);
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		public event EventHandler<GenericSingleEventArgs<string>> HoursUsedChanged;

		public event EventHandler<GenericSingleEventArgs<string>> PowerChanged;

		public event EventHandler<GenericSingleEventArgs<string>> VideoBlankChanged;

		public event EventHandler<GenericSingleEventArgs<string>> VideoFreezeChanged;

		public event EventHandler<GenericSingleEventArgs<string>> ConnectionChanged;

		public bool EnableReconnect
		{
			get
			{
				if (this.client == null)
				{
					return false;
				}

				Logger.Debug("PlanarSimplicityDisplayTcp {0} - get ReconnectEnabled", this.Id);
				return this.reconnectEnabled;
			}
			set
			{
				if (this.client == null)
				{
					return;
				}

				Logger.Debug("PlanarSimplicityDisplayTcp {0} - set ReconnectEnabled - {1}", this.Id, value);
				this.client.EnableReconnect = value;
				this.reconnectEnabled = value;
			}
		}

		public uint HoursUsed { get { return 0; } }

		public bool PowerState { get; private set; }

		public bool BlankState { get { return false; } }

		public bool SupportsFreeze { get { return false; } }

		public bool FreezeState { get { return false; } }

		public string Id { get; private set; }

		public bool IsInitialized { get; private set; }

		public bool IsOnline { get; private set; }

		public string Label { get; private set; }

		public void Initialize(string ipAddress, int port, string label, string id)
		{
			ParameterValidator.ThrowIfNullOrEmpty(ipAddress, "PlanarSimplicityDisplayTcp.Initialize", "ipAddress");
			ParameterValidator.ThrowIfNullOrEmpty(label, "PlanarSimplicityDisplayTcp.Initialize", "label");
			ParameterValidator.ThrowIfNullOrEmpty(id, "PlanarSimplicityDisplayTcp.Initialize", "id");
			if (port < 1)
			{
				throw new ArgumentException("PlanarSimplicityDisplayTcp.Initialize() - argument 'port' must be greater than zero.");
			}

			this.IsInitialized = false;

			this.Id = id;
			this.Label = label;

			if (this.client != null)
			{
				this.client.ClientConnected -= this.Client_ClientConnected;
				this.client.ConnectionFailed -= this.Client_ConnectionFailed;
				this.client.StatusChanged -= this.Client_StatusChanged;
				this.client.RxBytesRecieved -= this.Client_RxBytesRecieved;
				this.client.Dispose();
			}

			this.client = new BasicTcpClient(ipAddress, port, 1024);
			this.client.ClientConnected += this.Client_ClientConnected;
			this.client.ConnectionFailed += this.Client_ConnectionFailed;
			this.client.StatusChanged += this.Client_StatusChanged;
			this.client.RxBytesRecieved += this.Client_RxBytesRecieved;
			this.client.ReconnectTime = 4500;

			this.IsInitialized = true;
		}

		public void DisablePolling()
		{
			this.pollingEnabled = false;
			if (this.pollTimer != null)
			{
				this.pollTimer.Dispose();
				this.pollTimer = null;
			}
		}

		public void EnablePolling()
		{
			if (!this.IsOnline)
			{
				Logger.Debug("PlanarSimplicityDisplayTcp {0} - EnablePolling() - Not connected to the device.", this.Id);
				return;
			}

			this.pollingEnabled = true;
			if (this.pollTimer == null)
			{
				this.pollTimer = new CTimer(this.SendPollCommand, POLL_TIME);
			}
		}

		public void PowerOff()
		{
			Logger.Debug("PlanarSimplicityDisplayTcp {0} - PowerOff()", this.Id);
			if (client.Connected)
			{
				this.Send(TX_HEADER, POWER_OFF_CMD);
				this.PowerState = false;
				this.Notify(this.PowerChanged);
			}
		}

		public void PowerOn()
		{
			Logger.Debug("PlanarSimplicityDisplayTcp {0} - PowerOn()", this.Id);
			if (client.Connected)
			{
				this.Send(TX_HEADER, POWER_ON_CMD);
				this.PowerState = true;
				this.Notify(this.PowerChanged);
			}
		}

		public void Connect()
		{
			if (this.client == null || !this.IsInitialized)
			{
				Logger.Error("PlanarSimplicityDisplayTcp.Connect() - Object not initialized.");
				return;
			}

			if (!this.client.Connected)
			{
				this.client.Connect();
			}
		}

		public void Disconnect()
		{
			if (this.client == null || !this.IsInitialized)
			{
				Logger.Error("PlanarSimplicityDisplayTcp.Disconnect() - Object not initialized.");
				return;
			}

			if (this.client.Connected)
			{
				this.client.Disconnect();
			}
		}

		public void FreezeOff()
		{
			Logger.Warn("PlanarSimplicityDisplayTcp {0} - Freeze commands not supported.", this.Id);
		}

		public void FreezeOn()
		{
			Logger.Warn("PlanarSimplicityDisplayTcp {0} - Freeze commands not supported.", this.Id);
		}

		public void VideoBlankOff()
		{
			Logger.Warn("PlanarSimplicityDisplayTcp {0} - blank commands not supported.", this.Id);
		}

		public void VideoBlankOn()
		{
			Logger.Warn("PlanarSimplicityDisplayTcp {0} - blank commands not supported.", this.Id);
		}

		private void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				if (disposing)
				{
					if (this.pollTimer != null)
					{
						this.DisablePolling();
					}

					if (this.offlineTimer != null)
					{
						this.offlineTimer.Dispose();
						this.offlineTimer = null;
					}

					this.EnableReconnect = false;
					if (this.client != null)
					{
						this.client.Disconnect();
						this.client.Dispose();
						this.client = null;
					}
				}

				this.disposed = true;
			}
		}

		private void Client_RxBytesRecieved(object sender, GenericSingleEventArgs<byte[]> e)
		{
			if (e.Arg.Length < 7)
			{
				Logger.Warn("PlanarSimplicityDisplayTcp {0} - Incompleted RX received. Length = {1}", this.Id, e.Arg.Length);
				return;
			}

			if (e.Arg[6] == ACK_RX)
			{
				// ACK received
				this.HandleAckNakRx(e.Arg);
			}
			else if (e.Arg[6] == POWER_RX)
			{
				// power status rx received
				this.HandlePowerRx(e.Arg);
			}
			else
			{
				string rx = "";
				foreach (var b in e.Arg)
				{
					rx += string.Format("{0:X2} ", b);
				}

				Logger.Debug("PlanarSimplicityDisplayTcp {0} Unknown response received: {1}", this.Id, rx);
			}
		}

		private void Client_StatusChanged(object sender, EventArgs e)
		{
			Logger.Debug("PlanarSimplicityDisplayTcp {0} - client status changed: {1}", this.Id, this.client.ClientStatusMessage);
			if (client.Connected)
			{
				this.StopOfflineTimer();
				this.IsOnline = true;
				this.Notify(this.ConnectionChanged);
			}
			else
			{
				this.BeginOfflineTimer();
			}
		}

		private void HandleAckNakRx(byte[] rx)
		{
			Logger.Debug("PlanarSimplicityDisplayTcp {0} acknaknav response received: {1}", this.Id, rx[7]);
			switch (rx[7])
			{
				case 0x00:
					// ACK
					Logger.Debug("PlanarSimplicityDisplayTcp {0} - ACK received.", this.Id);
					break;
				case 0x01:
					//NAK
					Logger.Error("PlanarSimplicityDisplayTcp {0} - NAK response recieved", this.Id);
					break;
				case 0x04:
					// Not Available
					Logger.Error("PlanarSimplicityDisplayTcp {0} - Command not available response recieved", this.Id);
					break;
			}
		}

		private void HandlePowerRx(byte[] rx)
		{
			this.PowerState = rx[7] == 0x02;
			Logger.Debug("PlanarSimplicityDisplayTcp {0} - power arg = {1:X2}.", this.Id, rx[7]);
			this.Notify(this.PowerChanged);
		}

		private void Client_ConnectionFailed(object sender, GenericSingleEventArgs<SocketStatus> e)
		{
			Logger.Debug(
				"PlanarSimplicityDisplayTcp {0} - Connnection Failed: {1}",
				this.Id,
				e.Arg);

			this.IsOnline = false;
			this.BeginOfflineTimer();
			this.Notify(this.ConnectionChanged);
		}

		private void Client_ClientConnected(object sender, EventArgs e)
		{
			Logger.Debug("PlanarSimplicityDisplayTcp {0} - client connected.", this.Id);
			this.IsOnline = true;
			this.StopOfflineTimer();
			this.Notify(this.ConnectionChanged);
			this.SendPollCommand(null);
		}

		private void BeginOfflineTimer()
		{
			if (this.offlineTimerActive)
			{
				return;
			}

			this.offlineTimerActive = true;
			if (this.offlineTimer != null)
			{
				this.offlineTimer.Reset(OFFLINE_TIMEOUT);
			}
			else
			{
				this.offlineTimer = new CTimer((o) =>
				{
					Logger.Debug("PlanarSimplicityDisplayTcp {0} - Offline timer callback triggered.", this.Id);
					this.Notify(this.ConnectionChanged);
				}, OFFLINE_TIMEOUT);
			}
		}

		private void StopOfflineTimer()
		{
			this.offlineTimer?.Stop();

			this.offlineTimerActive = false;
		}

		private void Send(byte header, byte[] command)
		{
			Logger.Debug("PlanarSimplicityDisplayTcp.Send() for device {0}", this.Id);

			List<byte> cmd = new List<byte>() { header };
			foreach (var b in command)
			{
				cmd.Add(b);
			}

			byte[] convertedCmd = this.CalculateChecksumAndConvert(cmd);
			this.client.Send(convertedCmd);
		}

		private void SendPollCommand(object callbackObject)
		{
			Logger.Debug(string.Format("PlanarSimplicityDisplayTcp.SendPollCommand() for device {0}", this.Id));
			if (!this.IsOnline)
			{
				return;
			}

			this.Send(TX_HEADER, POWER_QUERY_CMD);
			if (this.pollingEnabled && this.pollTimer != null)
			{
				this.pollTimer.Reset(POLL_TIME);
			}
		}

		private byte[] CalculateChecksumAndConvert(List<byte> command)
		{
			byte checksum = 0x00;
			foreach (byte b in command)
			{
				checksum = (byte)(checksum ^ b);
			}

			command.Add(checksum);
			return command.ToArray();
		}

		private void Notify(EventHandler<GenericSingleEventArgs<string>> handler)
		{
			var temp = handler;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}
	}
}
