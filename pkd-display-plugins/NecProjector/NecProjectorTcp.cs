namespace NecProjector;

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
	private const int OfflineTimeout = 60000;
	private const int PollTime = 60000;

	private static readonly byte[] Inputs =
	[
		0xA1, // HDMI 1
		0xA2, // HDMI 2
		0xA6 // Display Port
	];

	private readonly Dictionary<byte, Action<byte[]>> _rxHandlers;
	private BasicTcpClient? _client;
	private CTimer? _pollTimer;
	private CTimer? _offlineTimer;
	private bool _pollingEnabled;
	private uint _selectedInput;
	private bool _disposed;
	private bool _tempFreezeState;
	private uint _tempSelectedInput;
	private bool _offlineTimerActive;

	/// <summary>
	/// Instantiates a new instance of <see cref="NecProjectorTcp"/>.  Call Initialize() to begin control.
	/// </summary>
	public NecProjectorTcp()
	{
		_rxHandlers = new Dictionary<byte, Action<byte[]>>()
		{
			{ 0x20, HandleQueryRx },
			{ 0x21, HandleFreezeRx },
			{ 0x22, CheckStateChangeRx },
			{ 0x23, HandleUseTimeRx },
			{ 0xA2, HandleStateError },
			{ 0xA0, HandleStateError },
			{ 0xA1, HandleFreezeError }
		};
	}

	~NecProjectorTcp()
	{
		Dispose(false);
	}

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? ConnectionChanged;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? HoursUsedChanged;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? PowerChanged;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? VideoBlankChanged;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? VideoFreezeChanged;

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, uint>>? VideoRouteChanged;

	/// <inheritdoc/>
	public uint HoursUsed { get; private set; }

	/// <inheritdoc/>
	public bool PowerState { get; private set; }

	/// <inheritdoc/>
	public bool SupportsFreeze => true;

	/// <inheritdoc/>
	public string Id { get; private set; } = string.Empty;

	/// <inheritdoc/>
	public bool IsOnline { get; private set; }

	/// <inheritdoc/>
	public string Label { get; private set; } = string.Empty;

	/// <inheritdoc/>
	public bool EnableReconnect
	{
		get => _client?.EnableReconnect ?? false;
		set
		{
			if (!CheckIfInitialized("EnableReconnect") || _client == null) return;
			_client.EnableReconnect = value;
		}
	}

	/// <inheritdoc/>
	public bool BlankState { get; private set; }

	/// <inheritdoc/>
	public bool FreezeState { get; private set; }

	public bool IsInitialized { get; private set; }

	/// <inheritdoc/>
	public void Initialize(string ipAddress, int port, string label, string id)
	{
		if (_client != null)
		{
			Logger.Warn("NecProjector.Initialize() - connection object already exists.");
			return;
		}

		IsInitialized = false;
		if (port <= 0)
		{
			throw new ArgumentException($"NecProjector.Initialize() - invalid port number: {port}");
		}

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

	/// <inheritdoc/>
	public void Connect()
	{
		CheckIfInitialized("Connect");
		_client?.Connect();
	}

	/// <inheritdoc/>
	public void Disconnect()
	{
		if (_client is { Connected: true })
		{
			_client.Disconnect();
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc/>
	public void PowerOff()
	{
		if (!CheckIfInitialized("PowerOff") || !IsOnline) return;
		var cmd = new List<byte>() { 0x02, 0x01, 0x00, 0x00, 0x00 };
		cmd.Add(CalculateChecksum(cmd));
		_client?.Send(cmd.ToArray());
	}

	/// <inheritdoc/>
	public void PowerOn()
	{
		if (!CheckIfInitialized("PowerOn") || !IsOnline) return;
		var cmd = new List<byte>() { 0x02, 0x00, 0x00, 0x00, 0x00 };
		cmd.Add(CalculateChecksum(cmd));
		_client?.Send(cmd.ToArray());
	}

	/// <inheritdoc/>
	public void FreezeOff()
	{
		if (!CheckIfInitialized("FreezeOff") || !IsOnline) return;
		_tempFreezeState = false;
		var cmd = new List<byte>() { 0x01, 0x98, 0x00, 0x00, 0x01, 0x02 };
		cmd.Add(CalculateChecksum(cmd));
		_client?.Send(cmd.ToArray());
	}

	/// <inheritdoc/>
	public void FreezeOn()
	{
		if (!CheckIfInitialized("FreezeOn") || !IsOnline) return;
		_tempFreezeState = true;
		var cmd = new List<byte>() { 0x01, 0x98, 0x00, 0x00, 0x01, 0x01 };
		cmd.Add(CalculateChecksum(cmd));
		_client?.Send(cmd.ToArray());
	}

	/// <inheritdoc/>
	public void VideoBlankOff()
	{
		if (!CheckIfInitialized("VideoBlankOff") || !IsOnline) return;
		var cmd = new List<byte>() { 0x02, 0x11, 0x00, 0x00, 0x00 };
		cmd.Add(CalculateChecksum(cmd));
		_client?.Send(cmd.ToArray());
	}

	/// <inheritdoc/>
	public void VideoBlankOn()
	{
		if (!CheckIfInitialized("VideoBlank") || !IsOnline) return;
		var cmd = new List<byte>() { 0x02, 0x10, 0x00, 0x00, 0x00 };
		cmd.Add(CalculateChecksum(cmd));
		_client?.Send(cmd.ToArray());
	}

	/// <inheritdoc/>
	public void EnablePolling()
	{
		if (!CheckIfInitialized("EnablePolling")) return;
		if (!IsOnline)
		{
			Logger.Error("NecProjectorTcp.EnablePolling() - Not connected to device, cannot enable polling.");
			return;
		}

		_pollTimer ??= new CTimer(SendPollCommand, PollTime);
		_pollingEnabled = true;
	}

	/// <inheritdoc/>
	public void DisablePolling()
	{
		_pollingEnabled = false;
		if (_pollTimer == null) return;
		_pollTimer.Dispose();
		_pollTimer = null;
	}

	/// <inheritdoc/>
	public uint GetCurrentVideoSource(uint output)
	{
		return _selectedInput;
	}

	/// <inheritdoc/>
	public void RouteVideo(uint source, uint output)
	{
		if (output > 1)
		{
			Logger.Error($"NecProjector.RouteVideo() - invalid output {output}");
			return;
		}

		if (source > Inputs.Length)
		{
			Logger.Error($"NecProjector.RouteVideo() - invalid source {source}");
			return;
		}

		if (!CheckIfInitialized("RouteVideo") || !IsOnline) return;
		_tempSelectedInput = source;
		var cmd = new List<byte>() { 0x02, 0x03, 0x00, 0x00, 0x02, 0x01, Inputs[source - 1] };
		cmd.Add(CalculateChecksum(cmd));
		_client?.Send(cmd.ToArray());
	}

	/// <inheritdoc/>
	public void ClearVideoRoute(uint output)
	{
		Logger.Info("NecProjector.ClearVideoRoute() - clearing input selection is not supported.");
	}

	private void NotifyOnlineStatusChanged()
	{
		var temp = ConnectionChanged;
		temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
	}

	private void BeginOfflineTimer()
	{
		if (_offlineTimerActive)
		{
			return;
		}

		Logger.Debug("NecProjectorTcp.BeginOfflineTimer()");
		_offlineTimerActive = true;
		if (_offlineTimer != null)
		{
			_offlineTimer.Reset(OfflineTimeout);
		}
		else
		{
			_offlineTimer = new CTimer(_ =>
			{
				Logger.Debug("NecProjectorTcp - Offline timer callback triggered.");
				NotifyOnlineStatusChanged();
			}, OfflineTimeout);
		}
	}

	private void StopOfflineTimer()
	{
		Logger.Debug("NecProjectorTcp.StopOfflineTimer()");
		_offlineTimer?.Stop();

		_offlineTimerActive = false;
	}

	private void Client_RxBytesReceived(object? sender, GenericSingleEventArgs<byte[]> e)
	{
		if (e.Arg.Length < 1)
		{
			return;
		}

		if (_rxHandlers.TryGetValue(e.Arg[0], out var found))
		{
			found.Invoke(e.Arg);
		}
		else if (e.Arg[0] != 0xA3) // lamp hours request fail, probably in standby
		{
			Logger.Warn($"NecProjector RX handler - Unknown header byte received: {e.Arg[0]:X2}");
		}
	}

	private void Client_StatusChanged(object? sender, EventArgs e)
	{
		IsOnline = _client?.Connected ?? false;
		if (IsOnline)
		{
			StopOfflineTimer();
			NotifyOnlineStatusChanged();
		}
		else
		{
			BeginOfflineTimer();
		}
	}

	private void Client_ConnectionFailed(object? sender, GenericSingleEventArgs<SocketStatus> e)
	{
		Logger.Debug($"NEC projector {Id} connection failed - {e.Arg}");

		IsOnline = false;
		BeginOfflineTimer();
		if (!EnableReconnect) return;
		Logger.Debug("NecProjector {0} - attempting reconnect...", Id);
		_client?.Connect();
	}

	private void Client_ClientConnected(object? sender, EventArgs e)
	{
		IsOnline = true;
		StopOfflineTimer();
		NotifyOnlineStatusChanged();
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
		if (_disposed) return;
		if (disposing)
		{
			if (_client != null)
			{
				_client.ClientConnected -= Client_ClientConnected;
				_client.ConnectionFailed -= Client_ConnectionFailed;
				_client.StatusChanged -= Client_StatusChanged;
				_client.RxBytesReceived -= Client_RxBytesReceived;
				_client.Dispose();
			}

			_pollTimer?.Dispose();
			_offlineTimer?.Dispose();
		}

		_disposed = true;
	}

	private void UpdatePowerState(bool newState)
	{
		if (newState == PowerState)
		{
			return;
		}

		PowerState = newState;
		Notify(this, PowerChanged);
	}

	private void UpdateBlankState(bool newState)
	{
		Logger.Debug("NecProjector {0} - UpdateBlankState({1})", Id, newState);

		if (newState == BlankState)
		{
			return;
		}

		BlankState = newState;
		Notify(this, VideoBlankChanged);
	}

	private void UpdateFreezeState(bool newState)
	{
		FreezeState = newState;
		Notify(this, VideoFreezeChanged);
	}

	private void UpdateInput(uint newInput)
	{
		if (newInput == _selectedInput)
		{
			return;
		}

		_selectedInput = newInput;
		var temp = VideoRouteChanged;
		temp?.Invoke(this, new GenericDualEventArgs<string, uint>(Id, 1));
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
			UpdatePowerState(cmd[6] == 0x04);
			UpdateBlankState(cmd[11] == 0x01);
			UpdateFreezeState(cmd[14] == 0x01);
		}
	}

	private void HandleFreezeRx(byte[] cmd)
	{
		if (cmd.Length < 6)
		{
			Logger.Error("NecProjector.HandleFreezeRx() - invalid command length.");
			return;
		}

		if (cmd[5] == 0x00)
		{
			UpdateFreezeState(_tempFreezeState);
		}
		else
		{
			Logger.Error("NecProjector.HandleFreezeRx() - Device was unable to modify display freeze status.");
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
				UpdatePowerState(true);
				break;
			case 0x01:
				// power off
				UpdatePowerState(false);
				break;
			case 0x03:
				// input changed
				HandleInputRx(cmd);
				break;
			case 0x10:
				// blank on
				UpdateBlankState(true);
				break;
			case 0x11:
				// blank off
				UpdateBlankState(false);
				break;
			default:
				ErrorLog.Error($"NecProjector.CheckStateChangeRx() - Unknown status byte: 0x{cmd[1]:X2}");
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
				if (hours != HoursUsed)
				{
					HoursUsed = hours;
					Notify(this, HoursUsedChanged);
				}

			}
			catch (Exception e)
			{
				Logger.Error($"NecProjector.handleUseTimeRx() - failed to parse time data: {e.Message}");
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
			UpdateInput(_tempSelectedInput);
		}
		else
		{
			Logger.Error("NecProjector.HandleInputRx() - Device was unable to change input selection.");
		}
	}

	private void SendPollCommand(object? callbackObject)
	{
		Logger.Debug($"NecProjectorTcp.SendPollCommand() for ID {Id}");

		if (!CheckIfInitialized("SendPollCommand") || !IsOnline) return;
		var cmd = new List<byte>() { 0x00, 0xBF, 0x00, 0x00, 0x01, 0x02 };
		var checkSum = CalculateChecksum(cmd);
		cmd.Add(checkSum);
		_client?.Send(cmd.ToArray());

		if (_pollTimer != null && _pollingEnabled)
		{
			_pollTimer.Reset(PollTime);
		}
	}

	private static void Notify(IDisplayDevice sender, EventHandler<GenericSingleEventArgs<string>>? handler)
	{
		handler?.Invoke(sender, new GenericSingleEventArgs<string>(sender.Id));
	}

	private bool CheckIfInitialized(string methodName)
	{
		if (!IsInitialized)
		{
			Logger.Error(($"NecProjector.{methodName}() Not yet initialized."));
		}
		return IsInitialized;
	}
}