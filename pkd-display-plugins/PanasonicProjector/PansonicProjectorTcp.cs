namespace PanasonicProjector;

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
	private const int PollTime = 60000;
	private const int RxTimeout = 3000;
	//private readonly string[] _inputCommands = ["00IIS:HD1\r", "00IIS:PC1\r"];
	//private readonly Dictionary<string, EventHandler<GenericSingleEventArgs<string>>?> _ackResponses = new Dictionary<string, EventHandler<GenericSingleEventArgs<string>>?>()
	// {
	// 	{ "00PON\r", PowerChanged },
	// 	{ "00POF\r", PowerChanged },
	// 	{ "00OSH:0\r", VideoBlankChanged },
	// 	{ "00OSH:1\r", VideoBlankChanged },
	// 	{ "00OFZ:0\r", VideoFreezeChanged },
	// 	{ "00OFZ:1\r", VideoFreezeChanged }
	// };
	private readonly Queue<CommandData> _cmdQueue;
	private readonly Dictionary<CommandTypes, Action<string>> _rxHandlers;
	private bool _disposed;
	private bool _pollingEnabled;
	private uint _offlineAttemptCounter;
	private bool _isSending;
	private bool _enabled;
	private BasicTcpClient? _client;
	private CTimer? _pollTimer;
	private CTimer? _rxTimer;

	/// <summary>
	/// Instantiates a new instance of <see cref="PanasonicProjectorTcp"/>. Call Initialize() to begin control.
	/// </summary>
	public PanasonicProjectorTcp()
	{
		_cmdQueue = new Queue<CommandData>();
		_rxHandlers = new Dictionary<CommandTypes, Action<string>>()
		{
			{ CommandTypes.Power, HandlePowerRx },
			{ CommandTypes.Blank, HandleBlankRx },
			{ CommandTypes.Freeze, HandleFreezeRx },
			{ CommandTypes.UseTime, HandleUseTimeRx },
			{ CommandTypes.Input, HandleInputRx },
		};
	}

	~PanasonicProjectorTcp()
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
	public uint HoursUsed { get; private set; }

	/// <inheritdoc/>
	public bool EnableReconnect
	{
		get => _client?.EnableReconnect ?? false;
		set
		{
			if (!IsInitialized || _client == null) return;
			_client.EnableReconnect = value;
		}
	}

	/// <inheritdoc/>
	public bool PowerState { get; private set; }

	/// <inheritdoc/>
	public bool SupportsFreeze => true;

	/// <inheritdoc/>
	public string Id { get; private set; } = string.Empty;

	/// <inheritdoc/>
	public bool IsOnline => _client is { Connected: true };

	/// <inheritdoc/>
	public string Label { get; private set; } = string.Empty;

	/// <inheritdoc/>
	public bool BlankState { get; private set; }

	/// <inheritdoc/>
	public bool FreezeState { get; private set; }

	/// <inheritdoc/>
	public bool IsInitialized { get; private set; }

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc/>
	public void Connect()
	{
		if (_client == null || _client.Connected) return;
		_enabled = true;
		_client.Connect();
	}

	/// <inheritdoc/>
	public void Disconnect()
	{
		_enabled = false;
		_client?.Disconnect();

		if (_rxTimer != null)
		{
			_rxTimer.Dispose();
			_rxTimer = null;
			_isSending = false;
		}

		_cmdQueue.Clear();
	}

	/// <inheritdoc/>
	public void EnablePolling()
	{
		if (!_enabled)
		{
			Logger.Error("NecProjectorTcp.EnablePolling() - control not enabled, cannot start polling.");
			return;
		}

		_pollingEnabled = true;
		if (_pollTimer == null)
		{
			_pollTimer = new CTimer(SendPollCommand, PollTime);
		}
		else
		{
			_pollTimer.Reset(PollTime);
		}
	}

	/// <inheritdoc/>
	public void DisablePolling()
	{
		_pollingEnabled = false;
		if (_pollTimer != null)
		{
			_pollTimer.Dispose();
			_pollTimer = null;
		}
	}

	/// <inheritdoc/>
	public void Initialize(string ipAddress, int port, string label, string id)
	{
		if (_client != null)
		{
			Logger.Warn("PanasonicProjectorTcp.Initialize() - connection object already exists.");
			return;
		}

		IsInitialized = false;
		if (port <= 0)
		{
			throw new ArgumentException($"PanasonicProjectorTcp.Initialize() - invalid port number: {port}");
		}

		Id = id;
		Label = label;

		if (_client != null)
		{
			_client.ClientConnected -= ClientConnectedHandler;
			_client.ConnectionFailed -= ClientConnectFailedHandler;
			_client.StatusChanged -= ClientStatusChangedHandler;
			_client.RxReceived -= ClientStringReceivedHandler;
			_client.Dispose();
		}

		_client = new BasicTcpClient(ipAddress, port, 2014);
		_client.ClientConnected += ClientConnectedHandler;
		_client.ConnectionFailed += ClientConnectFailedHandler;
		_client.StatusChanged += ClientStatusChangedHandler;
		_client.RxReceived += ClientStringReceivedHandler;
		_client.ReconnectTime = 4500;
		IsInitialized = true;
	}

	/// <inheritdoc/>
	public void PowerOff()
	{
		if (_enabled)
		{
			PowerState = false;
			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = "00POF\r",
				CommandType = CommandTypes.Power
			});

			TrySend();
		}
	}

	/// <inheritdoc/>
	public void PowerOn()
	{
		if (_enabled)
		{
			PowerState = true;
			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = "00PON\r",
				CommandType = CommandTypes.Power
			});

			TrySend();
		}
	}

	/// <inheritdoc/>
	public void FreezeOff()
	{
		if (_enabled && PowerState)
		{
			FreezeState = false;
			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = "OFZ:0\r",
				CommandType = CommandTypes.Freeze
			});

			TrySend();
		}
	}

	/// <inheritdoc/>
	public void FreezeOn()
	{
		if (_enabled && PowerState)
		{
			FreezeState = true;
			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = "OFZ:1\r",
				CommandType = CommandTypes.Freeze
			});

			TrySend();
		}
	}

	/// <inheritdoc/>
	public void VideoBlankOff()
	{
		if (_enabled && PowerState)
		{
			BlankState = false;
			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = "00OSH:0\r",
				CommandType = CommandTypes.Blank
			});

			TrySend();
		}
	}

	/// <inheritdoc/>
	public void VideoBlankOn()
	{
		if (_enabled && PowerState)
		{
			BlankState = true;
			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = "00OSH:1\r",
				CommandType = CommandTypes.Blank
			});

			TrySend();
		}
	}

	private void Dispose(bool disposing)
	{
		if (_disposed) return;
		if (disposing)
		{
			if (_client != null)
			{
				_client.ClientConnected -= ClientConnectedHandler;
				_client.ConnectionFailed -= ClientConnectFailedHandler;
				_client.StatusChanged -= ClientStatusChangedHandler;
				_client.RxReceived -= ClientStringReceivedHandler;
				_client.Dispose();
			}

			_pollTimer?.Dispose();
			_rxTimer?.Dispose();
		}

		_disposed = true;
	}

	private void SendPollCommand(object? callbackObject)
	{
		if (_enabled && _pollingEnabled)
		{
			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = "00QPW\r",
				CommandType = CommandTypes.Power
			});

			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = "00QSH\r",
				CommandType = CommandTypes.Blank
			});

			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = "00QFZ\r",
				CommandType = CommandTypes.Freeze
			});

			_cmdQueue.Enqueue(new CommandData()
			{
				CommandString = "00Q$L\r",
				CommandType = CommandTypes.UseTime
			});

			TrySend();
			if (_enabled && _pollingEnabled)
			{
				_pollTimer?.Reset(PollTime);
			}
		}
	}

	private void RxTimeoutHandler(object? callbackObject)
	{
		Logger.Error(
			"PanasonicProjector {0} - No response to command {1}. Removing from queue.",
			Id,
			_cmdQueue.Dequeue().CommandType);

		_isSending = false;
		TrySend();
	}

	private void ClientConnectedHandler(object? sender, EventArgs e)
	{
		Logger.Debug("PanasonicProjector.ClientConnectedHandler()");
		if (_offlineAttemptCounter > 1)
		{
			Notify(ConnectionChanged);
		}

		_offlineAttemptCounter = 0;
		TrySend();
	}

	private void ClientConnectFailedHandler(object? sender, GenericSingleEventArgs<SocketStatus> e)
	{
		Logger.Debug("PanasonicProjector.ClientConnectFailedHandler() - {0}", e.Arg);

		if (!_enabled)
		{
			_isSending = false;
			_offlineAttemptCounter = 0;
			_rxTimer?.Stop();
			Notify(ConnectionChanged);
		}
		else
		{
			_offlineAttemptCounter++;
			TrySend();
		}
	}

	private void ClientStatusChangedHandler(object? sender, EventArgs e)
	{
		if (!IsOnline)
		{
			_rxTimer?.Dispose();

			if (!_enabled)
			{
				_isSending = false;
				_cmdQueue.Clear();
			}
		}

		Notify(ConnectionChanged);
	}

	private void TrySend()
	{
		if (IsOnline)
		{
			if (!_isSending && _cmdQueue.Count > 0)
			{
				_isSending = true;
				var cmd = _cmdQueue.Peek();

				Logger.Debug("Panasonic Projector {0} - sending command {1}", Id, cmd.CommandString);
				_client?.Send(cmd.CommandString);

				if (_rxTimer == null)
				{
					_rxTimer = new CTimer(RxTimeoutHandler, RxTimeout);
				}
				else
				{
					_rxTimer.Reset(RxTimeout);
				}
			}
		}
	}

	private void ClientStringReceivedHandler(object? sender, GenericSingleEventArgs<string> e)
	{
		if (e.Arg.Contains("NTCONTROL")) return;
		_rxTimer?.Stop();
		var cmd = _cmdQueue.Dequeue();
		_rxHandlers[cmd.CommandType].Invoke(e.Arg);
		_isSending = false;
		TrySend();
	}

	private void HandlePowerRx(string rx)
	{
		if (!rx.Contains("PON") && !rx.Contains("POF") && !rx.Contains("ERR"))
		{
			try
			{
				short state = short.Parse(rx);
				PowerState = state > 0;
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

		Notify(PowerChanged);
	}

	private void HandleBlankRx(string rx)
	{
		Logger.Debug("PanasonicProjector {0} - HandleBlankRx({1})", Id, rx);

		if (!rx.Contains("OSH") && !rx.Contains("ERR"))
		{
			try
			{
				BlankState = short.Parse(rx) > 0;
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

		Notify(VideoBlankChanged);
	}

	private void HandleFreezeRx(string rx)
	{
		if (!rx.Contains("OFZ") && !rx.Contains("ERR"))
		{
			try
			{
				FreezeState = short.Parse(rx) > 0;
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

		Notify(VideoFreezeChanged);
	}

	private void HandleUseTimeRx(string rx)
	{
		if (!rx.Contains("ERR"))
		{
			try
			{
				HoursUsed = uint.Parse(rx);
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

		Notify(HoursUsedChanged);
	}

	private static void HandleInputRx(string rx) { }

	private void Notify(EventHandler<GenericSingleEventArgs<string>>? handler)
	{
		handler?.Invoke(this, new GenericSingleEventArgs<string>(Id));
	}
}