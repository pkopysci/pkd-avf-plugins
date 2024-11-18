namespace NecMultisync
{
	using Crestron.SimplSharp;
	using Crestron.SimplSharp.CrestronSockets;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_common_utils.NetComs;
	using pkd_common_utils.Validation;
	using pkd_hardware_service.DisplayDevices;
	using pkd_hardware_service.Routable;
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Display control plugin used with the GCU C# Framework. This plugin is for basic controls
	/// of an NEC Multisync display, such as the V552 or V554.
	/// Display ID must be 1 for commands to work.
	/// 
	/// Inputs: 1 = HDMI 1, 2 = HDMI 2, 3 = display port.
	/// </summary>
	public class NecMultisyncDisplayTcp : IDisplayDevice, IVideoRoutable, IDisposable
	{
		private static readonly int OFFLINE_TIMEOUT = 60000;
		private static readonly int POLL_TIME = 60000;

		private readonly Dictionary<InputRxTypes, Action> InputNotifications;

		private bool disposed;
		private BasicTcpClient client;
		private CTimer pollTimer;
		private CTimer offlineTimer;
		private bool offlineTimerActive;
		private bool pollingEnabled;
		private int currentVideoSource;

		/// <summary>
		/// Instantiates an instance of <see cref="NecMultisyncDisplayTcp"/>.
		/// </summary>
		public NecMultisyncDisplayTcp()
		{
			this.InputNotifications = new Dictionary<InputRxTypes, Action>()
			{
				{ InputRxTypes.AckHdmi1, this.UpdateInputHdmi1 },
				{ InputRxTypes.AckHdmi2, this.UpdateInputHdmi2 },
				{ InputRxTypes.AckDp, this.UpdateInputDisplayPort },
				{ InputRxTypes.QueryHdmi1, this.UpdateInputHdmi1 },
				{ InputRxTypes.QueryHdmi2, this.UpdateInputHdmi2 },
				{ InputRxTypes.QueryDp, this.UpdateInputDisplayPort }
			};
		}

		/// <summary>
		/// Finalizes this instance of <see cref="NecMultisyncDisplayTcp"/>.
		/// </summary>
		~NecMultisyncDisplayTcp()
		{
			this.Dispose(false);
		}

		/// <summary>
		/// Not supported by display devices.
		/// </summary>
		public event EventHandler<GenericSingleEventArgs<string>> VideoBlankChanged;

		/// <summary>
		/// Not supported by display devices.
		/// </summary>
		public event EventHandler<GenericSingleEventArgs<string>> HoursUsedChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> PowerChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> ConnectionChanged;

		/// <summary>
		/// Not supported by display devices.
		/// </summary>
		public event EventHandler<GenericSingleEventArgs<string>> VideoFreezeChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, uint>> VideoRouteChanged;

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
		public uint HoursUsed { get; private set; }

		/// <inheritdoc/>
		public bool PowerState { get; private set; }

		/// <inheritdoc/>
		public bool SupportsFreeze { get { return false; } }

		/// <inheritdoc/>
		public bool FreezeState { get { return false; } }

		/// <inheritdoc/>
		public string Id { get; private set; }

		/// <inheritdoc/>
		public bool IsInitialized { get; private set; }

		/// <inheritdoc/>
		public bool IsOnline { get { return this.client.Connected; } }

		/// <inheritdoc/>
		public string Label { get; private set; }

		/// <inheritdoc/>
		public bool BlankState { get; private set; }

		/// <inheritdoc/>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc/>
		public void Initialize(string ipAddress, int port, string label, string id)
		{
			ParameterValidator.ThrowIfNullOrEmpty(ipAddress, "NecMultisyncDisplayTcp.Initialize", "ipAddress");
			ParameterValidator.ThrowIfNullOrEmpty(label, "NecMultisyncDisplayTcp.Initialize", "label");
			ParameterValidator.ThrowIfNullOrEmpty(id, "NecMultisyncDisplayTcp.Initialize", "id");
			if (port < 1)
			{
				throw new ArgumentException("NecMultisyncDisplayTcp.Initialize() - argument 'port' must be greater than zero.");
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
		public void PowerOff()
		{
			if (this.IsOnline)
			{
				this.client.Send(MultisyncRxTools.PowerOffCommand);
			}
		}

		/// <inheritdoc/>
		public void PowerOn()
		{
			if (this.IsOnline)
			{
				this.client.Send(MultisyncRxTools.PowerOnCommand);
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
		public void EnablePolling()
		{
			if (!this.IsOnline)
			{
				Logger.Debug("NecMultisyncDisplayTcp {0} - EnablePolling() - Not connected to the device.", this.Id);
				return;
			}

			this.pollingEnabled = true;
			if (this.pollTimer == null)
			{
				this.pollTimer = new CTimer(this.SendPollCommand, POLL_TIME);
			}
		}

		/// <inheritdoc/>
		public void Connect()
		{
			if (this.client == null || !this.IsInitialized)
			{
				Logger.Error("NecMultisyncDisplayTcp.Connect() - Object not initialized.");
				return;
			}

			if (!this.client.Connected)
			{
				this.client.Connect();
			}
		}

		/// <inheritdoc/>
		public void Disconnect()
		{
			if (this.client == null || !this.IsInitialized)
			{
				Logger.Error("NecMultisyncDisplayTcp.Disconnect() - Object not initialized.");
				return;
			}

			if (this.client.Connected)
			{
				this.client.Disconnect();
			}
		}

		/// <summary>
		/// Not Supported by display devices.
		/// </summary>
		public void FreezeOff() { }

		/// <summary>
		/// Not Supported by display devices.
		/// </summary>
		public void FreezeOn() { }

		/// <summary>
		/// Not Supported by display devices.
		/// </summary>
		public void VideoBlankOff() { }

		/// <summary>
		/// Not Supported by display devices.
		/// </summary>
		public void VideoBlankOn() { }

		/// <summary>
		/// Not Supported by display devices.
		/// </summary>
		/// <param name="output"></param>
		public void ClearVideoRoute(uint output) { }

		/// <inheritdoc/>
		public uint GetCurrentVideoSource(uint output)
		{
			return (uint)this.currentVideoSource;
		}

		/// <inheritdoc/>
		public void RouteVideo(uint source, uint output)
		{
			var idx = (int)source;
			if (idx > MultisyncRxTools.InputSelectCommands.Count)
			{
				Logger.Error("NecMultisyncDisplayTcp {0} - RouteVide({1}, {2}) - input out of range.", this.Id, source, output);
				return;
			}

			if (!this.IsOnline)
			{
				Logger.Error("NecMultisyncDisplayTcp {0} - RouteVideo({1}, {2}) - Not connected to the device.", this.Id, source, output);
				return;
			}

			this.currentVideoSource = idx;
			this.client.Send(MultisyncRxTools.InputSelectCommands[idx]);
		}

		/******************************************************************
         * PRIVATE UTILITY METHODS
         *****************************************************************/

		private void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				if (disposing)
				{
					// TODO: dispose all the things
				}

				this.disposed = true;
			}
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
					Logger.Debug("NecProjectorTcp - Offline timer callback triggered.");
					this.Notify(this.ConnectionChanged);
				}, OFFLINE_TIMEOUT);
			}
		}

		private void StopOfflineTimer()
		{
			this.offlineTimer?.Stop();

			this.offlineTimerActive = false;
		}

		private void SendPollCommand(object callbackObj)
		{
			if (!this.IsOnline || !this.pollingEnabled)
			{
				return;
			}

			this.client.Send(MultisyncRxTools.PowerQueryCommand);
			this.client.Send(MultisyncRxTools.InputQueryCommand);
			this.pollTimer.Reset(POLL_TIME);
		}

		private bool TryHandlePowerRx(byte[] rxCommand)
		{
			byte[] header = new byte[rxCommand.Length - 1];
			Array.Copy(rxCommand, 0, header, 0, header.Length);
			bool isPowerRx = MultisyncRxTools.CompareByteArrays(header, MultisyncRxTools.powerSetRxHeader)
				|| MultisyncRxTools.CompareByteArrays(header, MultisyncRxTools.PowerGetRxHeader);

			if (!isPowerRx)
			{
				return false;
			}

			switch (rxCommand[rxCommand.Length - 1])
			{
				case MultisyncRxTools.PowerOnByte:
					this.PowerState = true;
					this.Notify(this.PowerChanged);
					break;
				case MultisyncRxTools.PowerOffByte:
				case MultisyncRxTools.PowerStandbyByte:
					this.PowerState = false;
					this.Notify(this.PowerChanged);
					break;
				default:
					Logger.Error("NecMultisyncDisplayTcp {0} - Unknown power status byte: {0:X2}", this.Id);
					break;
			}

			return true;
		}

		private bool TryHandleInputRx(byte[] rxCommand)
		{
			if (rxCommand.Length < 22) { return false; }

			try
			{
				byte[] data = new byte[4];
				Array.Copy(rxCommand, 22, data, 0, 4);
				foreach (var kvp in MultisyncRxTools.InputRxData)
				{
					if (MultisyncRxTools.CompareByteArrays(kvp.Value, data))
					{
						this.InputNotifications[kvp.Key].Invoke();
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Error("NecMultisyncDisplay {0} - Failed to parse input RX: {1}", this.Id, ex.Message);
			}

			return false;
		}

		private void Client_RxBytesRecieved(object sender, GenericSingleEventArgs<byte[]> e)
		{
			var rx = e.Arg;
			if (rx.Length < 5)
			{
				Logger.Error("NecMultisyncDisplayTcp {0} - bad response length.", this.Id);
				return;
			}

			byte[] commandData = MultisyncRxTools.GetCommand(rx);
			if (commandData.Length <= 0)
			{
				Logger.Error("NecMultisyncDisplayTcp {0} - Command data is empty.", this.Id);
				return;
			}

			if (!this.TryHandlePowerRx(commandData))
			{
				this.TryHandleInputRx(rx);
			}
		}

		private void Client_StatusChanged(object sender, EventArgs e)
		{
			Logger.Debug("NecMultisyncDisplayTcp {0} - client status changed: {1}", this.Id, this.client.ClientStatusMessage);
			if (this.IsOnline)
			{
				this.StopOfflineTimer();
				this.Notify(this.ConnectionChanged);
			}
			else
			{
				this.BeginOfflineTimer();
			}
		}

		private void Client_ConnectionFailed(object sender, GenericSingleEventArgs<SocketStatus> e)
		{
			Logger.Debug(
				"NecMultisyncDisplayTcp {0} - Connnection Failed: {1}",
				this.Id,
				e.Arg);

			this.BeginOfflineTimer();
		}

		private void Client_ClientConnected(object sender, EventArgs e)
		{
			this.StopOfflineTimer();
			this.Notify(this.ConnectionChanged);

			this.client.Send(MultisyncRxTools.PowerQueryCommand);
			this.client.Send(MultisyncRxTools.InputQueryCommand);
		}

		private void Notify(EventHandler<GenericSingleEventArgs<string>> handler)
		{
			var temp = handler;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}

		private void Notify(EventHandler<GenericDualEventArgs<string, uint>> handler)
		{
			var temp = handler;
			temp?.Invoke(this, new GenericDualEventArgs<string, uint>(this.Id, 1));
		}

		private void UpdateInputHdmi1()
		{
			this.currentVideoSource = 1;
			this.Notify(this.VideoRouteChanged);
		}

		private void UpdateInputHdmi2()
		{
			this.currentVideoSource = 2;
			this.Notify(this.VideoRouteChanged);
		}

		private void UpdateInputDisplayPort()
		{
			this.currentVideoSource = 3;
			this.Notify(this.VideoRouteChanged);
		}
	}
}
