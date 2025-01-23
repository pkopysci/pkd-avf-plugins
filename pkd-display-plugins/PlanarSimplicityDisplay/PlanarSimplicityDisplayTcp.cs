namespace PlanarSimplicityDisplay;

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
	private const int PollTime = 30000;
	private const int OfflineTimeout = 60000;
	private const byte TxHeader = 0xA6;
	private const byte AckRx = 0x00;
	private const byte PowerRx = 0x19;
	private static readonly byte[] PowerQueryCmd = [0x01, 0x00, 0x00, 0x00, 0x03, 0x01, 0x19];
	private static readonly byte[] PowerOnCmd = [0x01, 0x00, 0x00, 0x00, 0x04, 0x01, 0x18, 0x02];
	private static readonly byte[] PowerOffCmd = [0x01, 0x00, 0x00, 0x00, 0x04, 0x01, 0x18, 0x01];

	private bool _disposed;
	private bool _pollingEnabled;
	private bool _offlineTimerActive;
	private bool _reconnectEnabled;
	private BasicTcpClient? _client;
	private CTimer? _pollTimer;
	private CTimer? _offlineTimer;

	~PlanarSimplicityDisplayTcp()
	{
		Dispose(false);
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	public event EventHandler<GenericSingleEventArgs<string>>? HoursUsedChanged;

	public event EventHandler<GenericSingleEventArgs<string>>? PowerChanged;

	public event EventHandler<GenericSingleEventArgs<string>>? VideoBlankChanged;

	public event EventHandler<GenericSingleEventArgs<string>>? VideoFreezeChanged;

	public event EventHandler<GenericSingleEventArgs<string>>? ConnectionChanged;

	public bool EnableReconnect
	{
		get
		{
			if (_client == null)
			{
				return false;
			}

			Logger.Debug("PlanarSimplicityDisplayTcp {0} - get ReconnectEnabled", Id);
			return _reconnectEnabled;
		}
		set
		{
			if (_client == null)
			{
				return;
			}

			Logger.Debug("PlanarSimplicityDisplayTcp {0} - set ReconnectEnabled - {1}", Id, value);
			_client.EnableReconnect = value;
			_reconnectEnabled = value;
		}
	}

	public uint HoursUsed => 0;

	public bool PowerState { get; private set; }

	public bool BlankState => false;

	public bool SupportsFreeze => false;

	public bool FreezeState => false;

	public string Id { get; private set; } = string.Empty;

	public bool IsInitialized { get; private set; }

	public bool IsOnline { get; private set; }

	public string Label { get; private set; } = string.Empty;

	public void Initialize(string ipAddress, int port, string label, string id)
	{
		ParameterValidator.ThrowIfNullOrEmpty(ipAddress, "PlanarSimplicityDisplayTcp.Initialize", "ipAddress");
		ParameterValidator.ThrowIfNullOrEmpty(label, "PlanarSimplicityDisplayTcp.Initialize", "label");
		ParameterValidator.ThrowIfNullOrEmpty(id, "PlanarSimplicityDisplayTcp.Initialize", "id");
		if (port < 1)
		{
			throw new ArgumentException("PlanarSimplicityDisplayTcp.Initialize() - argument 'port' must be greater than zero.");
		}

		IsInitialized = false;

		Id = id;
		Label = label;

		if (_client != null)
		{
			_client.ClientConnected -= Client_ClientConnected;
			_client.ConnectionFailed -= Client_ConnectionFailed;
			_client.StatusChanged -= Client_StatusChanged;
			_client.RxBytesReceived -= Client_RxBytesReceived;
			_client.Dispose();
		}

		_client = new BasicTcpClient(ipAddress, port, 1024);
		_client.ClientConnected += Client_ClientConnected;
		_client.ConnectionFailed += Client_ConnectionFailed;
		_client.StatusChanged += Client_StatusChanged;
		_client.RxBytesReceived += Client_RxBytesReceived;
		_client.ReconnectTime = 4500;

		IsInitialized = true;
	}

	public void DisablePolling()
	{
		_pollingEnabled = false;
		if (_pollTimer == null) return;
		_pollTimer.Dispose();
		_pollTimer = null;
	}

	public void EnablePolling()
	{
		if (!CheckIfInitialized("EnablePolling")) return;
		if (!IsOnline)
		{
			Logger.Debug("PlanarSimplicityDisplayTcp {0} - EnablePolling() - Not connected to the device.", Id);
			return;
		}

		_pollingEnabled = true;
		_pollTimer ??= new CTimer(SendPollCommand, PollTime);
	}

	public void PowerOff()
	{
		if (!CheckIfInitialized("PowerOff")) return;
		if (!IsOnline)
		{
			Logger.Debug("PlanarSimplicityDisplayTcp {0} - PowerOff() - Not connected to the device.", Id);
			return;
		}
		
		Send(TxHeader, PowerOffCmd);
		PowerState = false;
		Notify(PowerChanged);
	}

	public void PowerOn()
	{
		if (!CheckIfInitialized("PowerOn")) return;
		if (!IsOnline)
		{
			Logger.Debug("PlanarSimplicityDisplayTcp {0} - PowerOn() - Not connected to the device.", Id);
			return;
		}
		
		Send(TxHeader, PowerOnCmd);
		PowerState = true;
		Notify(PowerChanged);
	}

	public void Connect()
	{
		if(!CheckIfInitialized("Connect") || IsOnline) return;
		_client?.Connect();
	}

	public void Disconnect()
	{
		if(!CheckIfInitialized("Disconnect") || !IsOnline) return;
		_client?.Disconnect();
	}

	public void FreezeOff()
	{
		Logger.Warn("PlanarSimplicityDisplayTcp {0} - Freeze commands not supported.", Id);
	}

	public void FreezeOn()
	{
		Logger.Warn("PlanarSimplicityDisplayTcp {0} - Freeze commands not supported.", Id);
	}

	public void VideoBlankOff()
	{
		Logger.Warn("PlanarSimplicityDisplayTcp {0} - blank commands not supported.", Id);
	}

	public void VideoBlankOn()
	{
		Logger.Warn("PlanarSimplicityDisplayTcp {0} - blank commands not supported.", Id);
	}

	private void Dispose(bool disposing)
	{
		if (_disposed) return;
		if (disposing)
		{
			if (_pollTimer != null)
			{
				DisablePolling();
			}

			if (_offlineTimer != null)
			{
				_offlineTimer.Dispose();
				_offlineTimer = null;
			}

			EnableReconnect = false;
			if (_client != null)
			{
				_client.Disconnect();
				_client.Dispose();
				_client = null;
			}
		}

		_disposed = true;
	}

	private void Client_RxBytesReceived(object? sender, GenericSingleEventArgs<byte[]> e)
	{
		if (e.Arg.Length < 7)
		{
			Logger.Warn("PlanarSimplicityDisplayTcp {0} - incomplete RX received. Length = {1}", Id, e.Arg.Length);
			return;
		}

		switch (e.Arg[6])
		{
			case AckRx:
				// ACK received
				HandleAckNakRx(e.Arg);
				break;
			case PowerRx:
				// power status rx received
				HandlePowerRx(e.Arg);
				break;
			default:
			{
				var rx = "";
				foreach (var b in e.Arg)
				{
					rx += $"{b:X2} ";
				}

				Logger.Debug("PlanarSimplicityDisplayTcp {0} Unknown response received: {1}", Id, rx);
				break;
			}
		}
	}

	private void Client_StatusChanged(object? sender, EventArgs e)
	{
		if (_client == null) return;
		if (_client.Connected)
		{
			StopOfflineTimer();
			IsOnline = true;
			Notify(ConnectionChanged);
		}
		else
		{
			BeginOfflineTimer();
		}
	}

	private void HandleAckNakRx(byte[] rx)
	{
		Logger.Debug("PlanarSimplicityDisplayTcp {0} acknaknav response received: {1}", Id, rx[7]);
		switch (rx[7])
		{
			case 0x00:
				// ACK
				Logger.Debug("PlanarSimplicityDisplayTcp {0} - ACK received.", Id);
				break;
			case 0x01:
				//NAK
				Logger.Error("PlanarSimplicityDisplayTcp {0} - NAK response received", Id);
				break;
			case 0x04:
				// Not Available
				Logger.Error("PlanarSimplicityDisplayTcp {0} - Command not available response received", Id);
				break;
		}
	}

	private void HandlePowerRx(byte[] rx)
	{
		PowerState = rx[7] == 0x02;
		Logger.Debug("PlanarSimplicityDisplayTcp {0} - power arg = {1:X2}.", Id, rx[7]);
		Notify(PowerChanged);
	}

	private void Client_ConnectionFailed(object? sender, GenericSingleEventArgs<SocketStatus> e)
	{
		Logger.Debug(
			"PlanarSimplicityDisplayTcp {0} - Connection Failed: {1}",
			Id,
			e.Arg);

		IsOnline = false;
		BeginOfflineTimer();
		Notify(ConnectionChanged);
	}

	private void Client_ClientConnected(object? sender, EventArgs e)
	{
		Logger.Debug("PlanarSimplicityDisplayTcp {0} - client connected.", Id);
		IsOnline = true;
		StopOfflineTimer();
		Notify(ConnectionChanged);
		SendPollCommand(null);
	}

	private void BeginOfflineTimer()
	{
		if (_offlineTimerActive)
		{
			return;
		}

		_offlineTimerActive = true;
		if (_offlineTimer != null)
		{
			_offlineTimer.Reset(OfflineTimeout);
		}
		else
		{
			_offlineTimer = new CTimer(_ =>
			{
				Logger.Debug("PlanarSimplicityDisplayTcp {0} - Offline timer callback triggered.", Id);
				Notify(ConnectionChanged);
			}, OfflineTimeout);
		}
	}

	private void StopOfflineTimer()
	{
		_offlineTimer?.Stop();

		_offlineTimerActive = false;
	}

	private void Send(byte header, byte[] command)
	{
		Logger.Debug("PlanarSimplicityDisplayTcp.Send() for device {0}", Id);

		List<byte> cmd = [];
		foreach (var b in command)
		{
			cmd.Add(b);
		}

		var convertedCmd = CalculateChecksumAndConvert(cmd);
		_client?.Send(convertedCmd);
	}

	private void SendPollCommand(object? callbackObject)
	{
		Logger.Debug($"PlanarSimplicityDisplayTcp.SendPollCommand() for device {Id}");
		if (!IsOnline)
		{
			return;
		}

		Send(TxHeader, PowerQueryCmd);
		if (_pollingEnabled && _pollTimer != null)
		{
			_pollTimer.Reset(PollTime);
		}
	}

	private static byte[] CalculateChecksumAndConvert(List<byte> command)
	{
		byte checksum = 0x00;
		foreach (var b in command)
		{
			checksum = (byte)(checksum ^ b);
		}

		command.Add(checksum);
		return command.ToArray();
	}

	private void Notify(EventHandler<GenericSingleEventArgs<string>>? handler)
	{
		handler?.Invoke(this, new GenericSingleEventArgs<string>(Id));
	}

	private bool CheckIfInitialized(string methodName)
	{
		if (!IsInitialized)
		{
			Logger.Error($"PlanarSimplicityDisplayTcp.{methodName}() - object not initialized.");
		}
		return IsInitialized;
	}
}