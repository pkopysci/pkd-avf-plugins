namespace NecProjector
{
	using Crestron.SimplSharp;
	using Crestron.SimplSharp.CrestronSockets;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_common_utils.NetComs;
	using pkd_hardware_service.DisplayDevices;
	using pkd_hardware_service.Routable;
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// GCU C# framework plugin for controlling an NEC projector.
	/// </summary>
	public class NecProjectorTcp : IDisplayDevice, IVideoRoutable, IDisposable
	{
		private static readonly int OFFLINE_TIMEOUT = 60000;
		private static readonly int POLL_TIME = 60000;
		private static readonly byte[] INPUTS = new byte[]
		{
			0xA1, // HDMI 1
            0xA2, // HDMI 2
            0xA6, // Display Port
        };

		private readonly Dictionary<byte, Action<byte[]>> rxHandlers;
		private BasicTcpClient client;
		private bool powerState;
		private bool blankState;
		private bool freezeState;
		private bool pollingEnabled;
		private uint selectedInput;
		private uint useTime;
		private bool disposed;
		private bool tempFreezeState;
		private uint tempselectedInput;
		private CTimer pollTimer;
		private CTimer offlineTimer;
		private bool offlineTimerActive;

		/// <summary>
		/// Instantiates a new instance of <see cref="NecProjectorTcp"/>.  Call Initialize() to begin control.
		/// </summary>
		public NecProjectorTcp()
		{
			this.rxHandlers = new Dictionary<byte, Action<byte[]>>()
			{
				{ 0x20, this.HandleQueryRx },
				{ 0x21, this.HandleFreezeRx },
				{ 0x22, this.CheckStateChangeRx },
				{ 0x23, this.HandleUseTimeRx },
				{ 0xA2, this.HandleStateError },
				{ 0xA0, this.HandleStateError },
				{ 0xA1, this.HandleFreezeError }
			};
		}

		~NecProjectorTcp()
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
		public event EventHandler<GenericDualEventArgs<string, uint>> VideoRouteChanged;

		/// <inheritdoc/>
		public uint HoursUsed
		{
			get { return this.useTime; }
		}

		/// <inheritdoc/>
		public bool PowerState
		{
			get { return this.powerState; }
		}

		/// <inheritdoc/>
		public bool SupportsFreeze
		{
			get { return true; }
		}

		/// <inheritdoc/>
		public string Id { get; set; }

		/// <inheritdoc/>
		public bool IsOnline { get; private set; }

		/// <inheritdoc/>
		public string Label { get; set; }

		/// <inheritdoc/>
		public bool EnableReconnect
		{
			get
			{
				return this.client.EnableReconnect;
			}

			set
			{
				Logger.Debug("NecProjectorTcp {0}. setting reconnect to {1}", this.Id, value);
				this.client.EnableReconnect = value;
			}
		}

		/// <inheritdoc/>
		public bool BlankState
		{
			get { return this.blankState; }
		}

		/// <inheritdoc/>
		public bool FreezeState
		{
			get { return this.freezeState; }
		}

		public bool IsInitialized { get; private set; }

		/// <inheritdoc/>
		public void Initialize(string ipAddress, int port, string label, string id)
		{
			if (client != null)
			{
				Logger.Warn("NecProjector.Initialize() - connection object already exists.");
				return;
			}

			this.IsInitialized = false;
			if (port <= 0)
			{
				throw new ArgumentException(string.Format(
					"NecProjector.Initialize() - invalid port number: {0}",
					port));
			}

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

		/// <inheritdoc/>
		public void Connect()
		{
			this.client.Connect();
		}

		/// <inheritdoc/>
		public void Disconnect()
		{
			if (this.client != null && this.client.Connected)
			{
				this.client.Disconnect();
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc/>
		public void PowerOff()
		{
			if (this.IsOnline)
			{
				List<byte> cmd = new List<byte>() { 0x02, 0x01, 0x00, 0x00, 0x00 };
				cmd.Add(this.CalculateChecksum(cmd));
				this.client.Send(cmd.ToArray());
			}
		}

		/// <inheritdoc/>
		public void PowerOn()
		{
			if (this.IsOnline)
			{
				List<byte> cmd = new List<byte>() { 0x02, 0x00, 0x00, 0x00, 0x00 };
				cmd.Add(this.CalculateChecksum(cmd));
				this.client.Send(cmd.ToArray());
			}
		}

		/// <inheritdoc/>
		public void FreezeOff()
		{
			if (this.IsOnline)
			{
				this.tempFreezeState = false;
				List<byte> cmd = new List<byte>() { 0x01, 0x98, 0x00, 0x00, 0x01, 0x02 };
				cmd.Add(this.CalculateChecksum(cmd));
				this.client.Send(cmd.ToArray());
			}
		}

		/// <inheritdoc/>
		public void FreezeOn()
		{
			if (this.IsOnline)
			{
				this.tempFreezeState = true;
				List<byte> cmd = new List<byte>() { 0x01, 0x98, 0x00, 0x00, 0x01, 0x01 };
				cmd.Add(this.CalculateChecksum(cmd));
				this.client.Send(cmd.ToArray());
			}
		}

		/// <inheritdoc/>
		public void VideoBlankOff()
		{
			if (this.IsOnline)
			{
				List<byte> cmd = new List<byte>() { 0x02, 0x11, 0x00, 0x00, 0x00 };
				cmd.Add(this.CalculateChecksum(cmd));
				this.client.Send(cmd.ToArray());
			}
		}

		/// <inheritdoc/>
		public void VideoBlankOn()
		{
			if (this.IsOnline)
			{
				List<byte> cmd = new List<byte>() { 0x02, 0x10, 0x00, 0x00, 0x00 };
				cmd.Add(this.CalculateChecksum(cmd));
				this.client.Send(cmd.ToArray());
			}
		}

		/// <inheritdoc/>
		public void EnablePolling()
		{
			if (!this.IsOnline)
			{
				Logger.Error("NecProjectorTcp.EnablePolling() - Not connected to device, cannot enable polling.");
				return;
			}

			this.pollingEnabled = true;
			if (this.pollTimer == null)
			{
				this.pollTimer = new CTimer(this.SendPollCommand, POLL_TIME);
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
		public uint GetCurrentVideoSource(uint output)
		{
			return (uint)this.selectedInput;
		}

		/// <inheritdoc/>
		public void RouteVideo(uint source, uint output)
		{
			if (output > 1)
			{
				Logger.Error(string.Format("NecProjector.RouteVideo() - invalid output {0}", output));
				return;
			}

			if (source > INPUTS.Length)
			{
				Logger.Error(string.Format("NecProjector.RouteVideo() - invalid source {0}", source));
				return;
			}

			if (this.IsOnline)
			{
				this.tempselectedInput = source;
				List<byte> cmd = new List<byte>() { 0x02, 0x03, 0x00, 0x00, 0x02, 0x01, INPUTS[source - 1] };
				cmd.Add(this.CalculateChecksum(cmd));
				this.client.Send(cmd.ToArray());
			}
		}

		/// <inheritdoc/>
		public void ClearVideoRoute(uint output)
		{
			Logger.Info("NecProjector.ClearVideoRoute() - clearing input selection is not supported.");
		}

		private void NotifyOnlineStatusChanged()
		{
			var temp = this.ConnectionChanged;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}

		private void BeginOfflineTimer()
		{
			if (this.offlineTimerActive)
			{
				return;
			}

			Logger.Debug("NecProjectorTcp.BeginOfflineTimer()");
			this.offlineTimerActive = true;
			if (this.offlineTimer != null)
			{
				this.offlineTimer.Reset(OFFLINE_TIMEOUT);
			}
			else
			{
				this.offlineTimer = new CTimer((o) =>
				{
					Logger.Debug("NecProjectorTcp - Offline timer callback triggered.");
					this.NotifyOnlineStatusChanged();
				}, OFFLINE_TIMEOUT);
			}
		}

		private void StopOfflineTimer()
		{
			Logger.Debug("NecProjectorTcp.StopOfflineTimer()");
			this.offlineTimer?.Stop();

			this.offlineTimerActive = false;
		}

		private void Client_RxBytesRecieved(object sender, GenericSingleEventArgs<byte[]> e)
		{
			if (e.Arg == null || e.Arg.Length < 1)
			{
				return;
			}

			if (this.rxHandlers.TryGetValue(e.Arg[0], out Action<byte[]> found))
			{
				found.Invoke(e.Arg);
			}
			else if (e.Arg[0] != 0xA3) // lamp hours request fail, probably in standby
			{
				Logger.Warn(string.Format(
					"NecProjector RX handler - Unknown header byte received: {0:X2}",
					e.Arg[0]));
			}
		}

		private void Client_StatusChanged(object sender, EventArgs e)
		{
			Logger.Debug("NecProjector.Client_StatusChanged: {0}", this.client.ClientStatusMessage);
			this.IsOnline = this.client.Connected;
			if (this.IsOnline)
			{
				this.StopOfflineTimer();
				this.NotifyOnlineStatusChanged();
			}
			else
			{
				this.BeginOfflineTimer();
			}
		}

		private void Client_ConnectionFailed(object sender, GenericSingleEventArgs<SocketStatus> e)
		{
			Logger.Debug(string.Format(
				"NEC projector {0} connection failed - {1}",
				this.Id,
				e.Arg));

			this.IsOnline = false;
			this.BeginOfflineTimer();

			if (this.EnableReconnect && !this.client.Connected)
			{
				Logger.Debug("NecProjector {0} - attempting reconnect...", this.Id);
				this.client.Connect();
			}
		}

		private void Client_ClientConnected(object sender, EventArgs e)
		{
			this.IsOnline = true;
			this.StopOfflineTimer();
			this.NotifyOnlineStatusChanged();
		}

		private byte CalculateChecksum(List<byte> command)
		{
			ushort checksum = 0;
			foreach (var b in command)
			{
				checksum += b;
			}

			return (byte)(checksum & 0xFF);
		}

		private void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				if (disposing)
				{
					if (this.client != null)
					{
						this.client.ClientConnected -= this.Client_ClientConnected;
						this.client.ConnectionFailed -= this.Client_ConnectionFailed;
						this.client.StatusChanged -= this.Client_StatusChanged;
						this.client.RxBytesRecieved -= this.Client_RxBytesRecieved;
						this.client.Dispose();
					}

					this.pollTimer?.Dispose();

					this.offlineTimer?.Dispose();
				}

				this.disposed = true;
			}
		}

		private void UpdatePowerState(bool newState)
		{
			if (newState == this.powerState)
			{
				return;
			}

			this.powerState = newState;
			Notify(this, this.PowerChanged);
		}

		private void UpdateBlankState(bool newState)
		{
			Logger.Debug("NecProjector {0} - UpdatBlankState({1})", this.Id, newState);

			if (newState == this.blankState)
			{
				return;
			}

			this.blankState = newState;
			Notify(this, this.VideoBlankChanged);
		}

		private void UpdateFreezeState(bool newState)
		{
			this.freezeState = newState;
			Notify(this, this.VideoFreezeChanged);
		}

		private void UpdateInput(uint newInput)
		{
			if (newInput == this.selectedInput)
			{
				return;
			}

			this.selectedInput = newInput;
			var temp = this.VideoRouteChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, uint>(this.Id, 1));
		}

		private void HandleQueryRx(byte[] cmd)
		{
			if (cmd.Length < 15)
			{
				Logger.Error("NecProjector.HandleQueryRx() - invalid command length.");
				return;
			}

			if (cmd[4] == 0x10)
			{
				this.UpdatePowerState(cmd[6] == 0x04);
				this.UpdateBlankState(cmd[11] == 0x01);
				this.UpdateFreezeState(cmd[14] == 0x01);
			}
		}

		private void HandleFreezeRx(byte[] cmd)
		{
			if (cmd.Length < 6)
			{
				Logger.Error("NecProjector.HandleReezeRx() - invalid command length.");
				return;
			}

			if (cmd[5] == 0x00)
			{
				this.UpdateFreezeState(this.tempFreezeState);
			}
			else
			{
				Logger.Error("NecProjector.HandleFreezeRx() - Device was unable to modifiy display freeze status.");
			}
		}

		private void CheckStateChangeRx(byte[] cmd)
		{
			if (cmd.Length < 2)
			{
				Logger.Error("NecProjector.CheckStateChangeRx(0 - Invalid command length.");
				return;
			}

			switch (cmd[1])
			{
				case 0x00:
					// power on
					this.UpdatePowerState(true);
					break;
				case 0x01:
					// power off
					this.UpdatePowerState(false);
					break;
				case 0x03:
					// input changed
					this.HandleInputRx(cmd);
					break;
				case 0x10:
					// blank on
					this.UpdateBlankState(true);
					break;
				case 0x11:
					// blank off
					this.UpdateBlankState(false);
					break;
				default:
					ErrorLog.Error(string.Format(
						"NecProjector.CheckStateChangeRx() - Unknown status byte: 0x{0:X2}",
						cmd[1]));
					break;
			}
		}

		private void HandleUseTimeRx(byte[] cmd)
		{
			if (cmd.Length < 11)
			{
				Logger.Error("NecProjector.HandleUseTimeRx() - Invalid command length.");
				return;
			}

			if (cmd[1] == 0x96)
			{
				try
				{
					byte[] timeBytes = new Byte[4];
					timeBytes[0] = cmd[7];
					timeBytes[1] = cmd[8];
					timeBytes[3] = cmd[9];
					timeBytes[4] = cmd[10];

					short seconds = BitConverter.ToInt16(timeBytes, 0);
					uint hours = (uint)(seconds / 3600);
					if (hours != this.useTime)
					{
						this.useTime = hours;
						Notify(this, this.HoursUsedChanged);
					}

				}
				catch (Exception e)
				{
					Logger.Error(string.Format(
						"NecProjector.handleUseTimeRx() - failed to parse time data: {0}",
						e.Message));
				}
			}
		}

		private void HandleStateError(byte[] cmd)
		{
			Logger.Error("NecProjector.HandleStateError() - State command or query error received.");
		}

		private void HandleFreezeError(byte[] cmd)
		{
			Logger.Error("NecProjector.HandleFreezeError() - Freeze command or query error received.");
		}

		private void HandleInputRx(byte[] cmd)
		{
			if (cmd.Length < 6)
			{
				Logger.Error("NecProjector.HandleInputRx() - invalid command length.");
				return;
			}

			if (cmd[5] == 0x00)
			{
				this.UpdateInput(this.tempselectedInput);
			}
			else
			{
				Logger.Error("NecProjector.HandleInputRx() - Device was unable to change input selection.");
			}
		}

		private void SendPollCommand(object callbackObject)
		{
			Logger.Debug(string.Format("NecProjectorTcp.SendPollCommand() for ID {0}", this.Id));

			if (this.IsOnline)
			{
				List<byte> cmd = new List<byte>() { 0x00, 0xBF, 0x00, 0x00, 0x01, 0x02 };
				byte checkSum = this.CalculateChecksum(cmd);
				cmd.Add(checkSum);
				this.client.Send(cmd.ToArray());

				if (this.pollTimer != null && this.pollingEnabled)
				{
					this.pollTimer.Reset(POLL_TIME);
				}
			}
		}

		private static void Notify(IDisplayDevice sender, EventHandler<GenericSingleEventArgs<string>> handler)
		{
			var temp = handler;
			temp?.Invoke(sender, new GenericSingleEventArgs<string>(sender.Id));
		}
	}
}
