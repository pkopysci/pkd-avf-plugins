namespace NecMultisyncDisplay;

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
	private const int OfflineTimeout = 60000;
	private const int PollTime = 60000;

	private readonly Dictionary<InputRxTypes, Action> _inputNotifications;

	private bool _disposed;
	private BasicTcpClient? _client;
	private CTimer? _pollTimer;
	private CTimer? _offlineTimer;
	private bool _offlineTimerActive;
	private bool _pollingEnabled;
	private int _currentVideoSource;

	/// <summary>
	/// Instantiates an instance of <see cref="NecMultisyncDisplayTcp"/>.
	/// </summary>
	public NecMultisyncDisplayTcp()
	{
		HoursUsed = 0;
		_inputNotifications = new Dictionary<InputRxTypes, Action>()
		{
			{ InputRxTypes.AckHdmi1, UpdateInputHdmi1 },
			{ InputRxTypes.AckHdmi2, UpdateInputHdmi2 },
			{ InputRxTypes.AckDp, UpdateInputDisplayPort },
			{ InputRxTypes.QueryHdmi1, UpdateInputHdmi1 },
			{ InputRxTypes.QueryHdmi2, UpdateInputHdmi2 },
			{ InputRxTypes.QueryDp, UpdateInputDisplayPort }
		};
	}

	/// <summary>
	/// Finalizes this instance of <see cref="NecMultisyncDisplayTcp"/>.
	/// </summary>
	~NecMultisyncDisplayTcp()
	{
		Dispose(false);
	}

	/// <summary>
	/// Not supported by display devices.
	/// </summary>
	public event EventHandler<GenericSingleEventArgs<string>>? VideoBlankChanged;

	/// <summary>
	/// Not supported by display devices.
	/// </summary>
	public event EventHandler<GenericSingleEventArgs<string>>? HoursUsedChanged;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? PowerChanged;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? ConnectionChanged;

	/// <summary>
	/// Not supported by display devices.
	/// </summary>
	public event EventHandler<GenericSingleEventArgs<string>>? VideoFreezeChanged;

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, uint>>? VideoRouteChanged;

	/// <inheritdoc/>
	public bool EnableReconnect
	{
		get => _client?.EnableReconnect ?? false;
		set
		{
			if (_client == null)
			{
				Logger.Error("NecMultisyncDisplayTcp - cannot set EnableReconnect before initializing.");
				return;
			}

			_client.EnableReconnect = value;
		}
	}

	/// NEC display does not support use tracking.
	public uint HoursUsed { get; }

	/// <inheritdoc/>
	public bool PowerState { get; private set; }

	/// <inheritdoc/>
	public bool SupportsFreeze => false;

	/// <inheritdoc/>
	public bool FreezeState => false;

	/// <inheritdoc/>
	public string Id { get; private set; } = string.Empty;

	/// <inheritdoc/>
	public bool IsInitialized { get; private set; }

	/// <inheritdoc/>
	public bool IsOnline => _client?.Connected ?? false;

	/// <inheritdoc/>
	public string Label { get; private set; } = string.Empty;

	/// <inheritdoc/>
	public bool BlankState => false;

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc/>
	public void Initialize(string ipAddress, int port, string label, string id)
	{
		ParameterValidator.ThrowIfNullOrEmpty(ipAddress, "NecMultisyncDisplayTcp.Initialize", nameof(ipAddress));
		ParameterValidator.ThrowIfNullOrEmpty(label, "NecMultisyncDisplayTcp.Initialize", nameof(label));
		ParameterValidator.ThrowIfNullOrEmpty(id, "NecMultisyncDisplayTcp.Initialize", nameof(id));
		if (port < 1)
		{
			throw new ArgumentException("NecMultisyncDisplayTcp.Initialize() - argument 'port' must be greater than zero.");
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
	public void PowerOff()
	{
		if (!CheckIfInitialized("PowerOff") || !IsOnline) return;
		_client?.Send(MultisyncRxTools.PowerOffCommand);
	}

	/// <inheritdoc/>
	public void PowerOn()
	{
		if (!CheckIfInitialized("PowerOn") || !IsOnline) return;
		_client?.Send(MultisyncRxTools.PowerOnCommand);
	}

	/// <inheritdoc/>
	public void DisablePolling()
	{
		if (_pollTimer != null)
		{
			_pollTimer.Dispose();
			_pollTimer = null;
		}
		
		_pollingEnabled = false;
	}

	/// <inheritdoc/>
	public void EnablePolling()
	{
		if (!CheckIfInitialized("EnablePolling") || !IsOnline) return;
		_pollTimer ??= new CTimer(SendPollCommand, PollTime);
		_pollingEnabled = true;
	}

	/// <inheritdoc/>
	public void Connect()
	{
		if (_client == null || !IsInitialized)
		{
			Logger.Error("NecMultisyncDisplayTcp.Connect() - Object not initialized.");
			return;
		}

		if (!_client.Connected)
		{
			_client.Connect();
		}
	}

	/// <inheritdoc/>
	public void Disconnect()
	{
		if (_client == null || !IsInitialized)
		{
			Logger.Error("NecMultisyncDisplayTcp.Disconnect() - Object not initialized.");
			return;
		}

		if (_client.Connected)
		{
			_client.Disconnect();
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
		return (uint)_currentVideoSource;
	}

	/// <inheritdoc/>
	public void RouteVideo(uint source, uint output)
	{
		if (!CheckIfInitialized("RouteVideo") || !IsOnline) return;
		var idx = (int)source;
		if (idx > MultisyncRxTools.InputSelectCommands.Count)
		{
			Logger.Error("NecMultisyncDisplayTcp {0} - RouteVide({1}, {2}) - input out of range.", Id, source, output);
			return;
		}

		if (!IsOnline)
		{
			Logger.Error("NecMultisyncDisplayTcp {0} - RouteVideo({1}, {2}) - Not connected to the device.", Id, source, output);
			return;
		}

		_currentVideoSource = idx;
		_client?.Send(MultisyncRxTools.InputSelectCommands[idx]);
	}

	/******************************************************************
	 * PRIVATE UTILITY METHODS
	 *****************************************************************/

	private bool CheckIfInitialized(string methodName)
	{
		if (!IsInitialized)
		{
			Logger.Error($"NecMultisyncDisplayTcp.{methodName}() - Object not initialized.");
		}

		return IsInitialized;
	}
	
	private void Dispose(bool disposing)
	{
		if (_disposed) return;
		if (disposing)
		{
			// TODO: dispose all the things
			_client?.Dispose();
			_pollTimer?.Dispose();
			_offlineTimer?.Dispose();
		}

		_disposed = true;
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
				Logger.Debug("NecProjectorTcp - Offline timer callback triggered.");
				Notify(ConnectionChanged);
			}, OfflineTimeout);
		}
	}

	private void StopOfflineTimer()
	{
		_offlineTimer?.Stop();

		_offlineTimerActive = false;
	}

	private void SendPollCommand(object? callbackObj)
	{
		if (!IsOnline || !_pollingEnabled)
		{
			return;
		}

		_client?.Send(MultisyncRxTools.PowerQueryCommand);
		_client?.Send(MultisyncRxTools.InputQueryCommand);
		_pollTimer?.Reset(PollTime);
	}

	private bool TryHandlePowerRx(byte[] rxCommand)
	{
		var header = new byte[rxCommand.Length - 1];
		Array.Copy(rxCommand, 0, header, 0, header.Length);
		var isPowerRx = MultisyncRxTools.CompareByteArrays(header, MultisyncRxTools.PowerSetRxHeader)
		                || MultisyncRxTools.CompareByteArrays(header, MultisyncRxTools.PowerGetRxHeader);

		if (!isPowerRx) return false;

		// index from end
		switch (rxCommand[^1])
		{
			case MultisyncRxTools.PowerOnByte:
				PowerState = true;
				Notify(PowerChanged);
				break;
			case MultisyncRxTools.PowerOffByte:
			case MultisyncRxTools.PowerStandbyByte:
				PowerState = false;
				Notify(PowerChanged);
				break;
			default:
				Logger.Error("NecMultisyncDisplayTcp {0} - Unknown power status byte: {0:X2}", Id);
				break;
		}

		return true;
	}

	private void HandleInputRx(byte[] rxCommand)
	{
		if (rxCommand.Length < 22) { return; }
		try
		{
			var data = new byte[4];
			Array.Copy(rxCommand, 22, data, 0, 4);
			foreach (var kvp in MultisyncRxTools.InputRxData)
			{
				if (MultisyncRxTools.CompareByteArrays(kvp.Value, data))
				{
					_inputNotifications[kvp.Key].Invoke();
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error("NecMultisyncDisplay {0} - Failed to parse input RX: {1}", Id, ex.Message);
		}
	}

	private void Client_RxBytesReceived(object? sender, GenericSingleEventArgs<byte[]> e)
	{
		var rx = e.Arg;
		if (rx.Length < 5)
		{
			Logger.Error("NecMultisyncDisplayTcp {0} - bad response length.", Id);
			return;
		}

		byte[] commandData = MultisyncRxTools.GetCommand(rx);
		if (commandData.Length <= 0)
		{
			Logger.Error("NecMultisyncDisplayTcp {0} - Command data is empty.", Id);
			return;
		}

		if (!TryHandlePowerRx(commandData))
		{
			HandleInputRx(rx);
		}
	}

	private void Client_StatusChanged(object? sender, EventArgs e)
	{
		if (IsOnline)
		{
			StopOfflineTimer();
			Notify(ConnectionChanged);
		}
		else
		{
			BeginOfflineTimer();
		}
	}

	private void Client_ConnectionFailed(object? sender, GenericSingleEventArgs<SocketStatus> e)
	{
		Logger.Debug(
			"NecMultisyncDisplayTcp {0} - Connection Failed: {1}",
			Id,
			e.Arg);

		BeginOfflineTimer();
	}

	private void Client_ClientConnected(object? sender, EventArgs e)
	{
		StopOfflineTimer();
		Notify(ConnectionChanged);

		_client?.Send(MultisyncRxTools.PowerQueryCommand);
		_client?.Send(MultisyncRxTools.InputQueryCommand);
	}

	private void Notify(EventHandler<GenericSingleEventArgs<string>>? handler)
	{
		handler?.Invoke(this, new GenericSingleEventArgs<string>(Id));
	}

	private void Notify(EventHandler<GenericDualEventArgs<string, uint>>? handler)
	{
		handler?.Invoke(this, new GenericDualEventArgs<string, uint>(Id, 1));
	}

	private void UpdateInputHdmi1()
	{
		_currentVideoSource = 1;
		Notify(VideoRouteChanged);
	}

	private void UpdateInputHdmi2()
	{
		_currentVideoSource = 2;
		Notify(VideoRouteChanged);
	}

	private void UpdateInputDisplayPort()
	{
		_currentVideoSource = 3;
		Notify(VideoRouteChanged);
	}
}