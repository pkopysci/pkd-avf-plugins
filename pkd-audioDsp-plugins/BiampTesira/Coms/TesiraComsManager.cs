using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using pkd_common_utils.Logging;


namespace BiampTesira.Coms;

public class TesiraComsManager(string hostname, int port, string username, string password)
    : IDisposable
{
    private readonly Queue<TesiraComsData> _commandQueue = [];
    private readonly ListBuffer<byte> _rxBuffer = new();
    private readonly object _lock = new();
    private TCPClient? _client;
    private bool _disposed;

    private static readonly Dictionary<byte, byte> HandshakeByteMap = new()
    {
        { 0xFF, 0xFF },
        { 0xFD, 0xFC },
        { 0xFB, 0xFE }
    };


    ~TesiraComsManager()
    {
        Dispose(false);
    }

    public event EventHandler? ConnectionChanged;
    public event EventHandler? ReadyForCommandsReceived;

    public bool Connected => _client is { ClientStatus : SocketStatus.SOCKET_STATUS_CONNECTED };
    public bool ReadyForCommands { get; private set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #region public methods

    public void Connect()
    {
        if (_client == null)
        {
            _client = new TCPClient(hostname, port, 5000);
            _client.SocketStatusChange += ClientStatusChangedHandler;
        }

        _client?.ConnectToServerAsync((_) => { });
    }

    public void Disconnect()
    {
        _client?.DisconnectFromServer();
        _client?.Dispose();
    }

    public void SendCommand(TesiraComsData commandData)
    {
        if (!Connected)
        {
            Logger.Error($"BiampTesira.TesiraComsManager {hostname}:{port} - EnqueueCommand() - client not connected.");
            return;
        }

        Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    _commandQueue.Enqueue(commandData);
                    Logger.Debug($"BiampTesira.TesiraComsManager.SendCommand() - {commandData.SerializedCommand}");
                    var chars = commandData.SerializedCommand.ToCharArray();
                    var bytes = new byte[chars.Length];
                    for (var i = 0; i < chars.Length; i++)
                    {
                        bytes[i] = (byte)chars[i];
                    }

                    _client?.SendData(bytes, bytes.Length);
                }
                catch (Exception e)
                {
                    Logger.Error(
                        $"BiampTesira.TesiraComsManager {hostname}:{port} - EnqueueCommand() exception: {e.Message} - {e.StackTrace?.Replace("\n", "\n\r")}");
                }
            }
        });
    }

    #endregion

    #region private methods

    private void ClientOnRxReceived(TCPClient client, int bytesReceived)
    {
        if (bytesReceived <= 0) return;
        lock (_lock)
        {
            try
            {
                var bytes = client.IncomingDataBuffer.Take(bytesReceived).ToArray();
                if (bytes[0] == 0xFF) // handshake request received
                {
                    CrestronConsole.PrintLine("Handshake received:");
                    foreach (var b in bytes)
                    {
                        CrestronConsole.Print($"{b:X2} ");
                    }

                    CrestronConsole.PrintLine(string.Empty);

                    var handshakeRx = GetHandshakeRx(bytes, bytesReceived);

                    CrestronConsole.PrintLine("Sending response:");
                    foreach (var b in handshakeRx)
                    {
                        CrestronConsole.Print($"{b:X2} ");
                    }

                    CrestronConsole.PrintLine("\n\r");

                    _client?.SendDataAsync(handshakeRx, handshakeRx.Length, (_, _) => { });
                }
                else if (CreateString(bytes).Contains("Welcome to the Tesira Text Protocol Server..."))
                {
                    ReadyForCommands = true;
                    ReadyForCommandsReceived?.Invoke(this, EventArgs.Empty);
                    // TODO: Handle prompts for login if authentication is enabled on the server.
                }
                else
                {
                    _rxBuffer.AddItems(bytes);
                    HandlePostHandshakeResponse();
                }
            }
            catch (Exception e)
            {
                Logger.Error(
                    $"BiampTesira.TesiraComsManager {hostname}:{port} - ClientOnRxReceived() - {e.Message}:{e.StackTrace?.Replace("\n", "\n\r")}");
            }
        }

        _client?.ReceiveDataAsync(ClientOnRxReceived);
    }

    /// <summary>
    /// Sends a handshake response to the Tesira telnet server. This response denies any telnet features and notifies the server
    /// that all commands and responses will be handled as raw TCP data.
    /// </summary>
    private static byte[] GetHandshakeRx(byte[] response, int bytesReceived)
    {
        var handshakeTx = new byte[bytesReceived];
        for (var i = 0; i < bytesReceived; i++)
        {
            handshakeTx[i] = HandshakeByteMap.TryGetValue(response[i], out var rxMapped) ? rxMapped : response[i];
        }

        return handshakeTx;
    }

    private void HandlePostHandshakeResponse()
    {
        // TODO: BiampTesiraDsp.TesiraComsManager.HandlePostHandshakeResponse()
        var delimiterIndex = _rxBuffer.GetIndexOfItem(0x0D);
        if (delimiterIndex < 0 || delimiterIndex + 1 > _rxBuffer.Length) return;

        var cmd = _rxBuffer.RemoveByLength(delimiterIndex + 1);
        if (cmd.Count == 0)
        {
            Logger.Debug(
                "BiampTesira.TesiraComsManager {hostname}:{port} -  HandlePostHandshakeResponse() - cmd is empty.");
        }
        else
        {
            var rx = CreateString(cmd.ToArray());
            if (rx.Contains("+OK"))
            {
                var command = _commandQueue.Dequeue();
                CrestronConsole.PrintLine($"dequeued command: {command.SerializedCommand}");
                CrestronConsole.PrintLine($"Received response: {rx}");
            }
            else if (rx.Contains("-ERR"))
            {
                var command = _commandQueue.Dequeue();
                CrestronConsole.PrintLine($"dequeued command: {command.SerializedCommand}");
                CrestronConsole.PrintLine($"Received response: {rx}");
            }
            else if (rx.Contains("! \"publishToken\":"))
            {
                var command = _commandQueue.Dequeue();
                CrestronConsole.PrintLine($"dequeued command: {command.SerializedCommand}");
                CrestronConsole.PrintLine($"Received response: {rx}");
            }
        }
    }

    private void ClientStatusChangedHandler(TCPClient client, SocketStatus socketStatus)
    {
        switch (socketStatus)
        {
            case SocketStatus.SOCKET_STATUS_CONNECTED:
                _client?.ReceiveDataAsync(ClientOnRxReceived);
                break;
            case SocketStatus.SOCKET_STATUS_NO_CONNECT:
                Logger.Debug("BiampTesira.TesiraComsManager.ClientStatusChangedHandler() - Client disconnected.");
                lock(_lock) _commandQueue.Clear();
                _rxBuffer.Clear();
                break;
            case SocketStatus.SOCKET_STATUS_BROKEN_REMOTELY:
                lock(_lock) _commandQueue.Clear();
                _rxBuffer.Clear();
                Logger.Error(
                    "BiampTesira.TesiraComsManager.ClientStatusChangedHandler() - Client disconnected remotely.");
                break;
            case SocketStatus.SOCKET_STATUS_CONNECT_FAILED:
            case SocketStatus.SOCKET_STATUS_DNS_FAILED:
                // TODO: BiampTesiraDsp.TesiraComsManager.ClientStatusChangedHandler - reconnect attempt on failure?
                Logger.Error(
                    $"BiampTesira.TesiraComsManager.ClientStatusChangedHandler(): Connection failed: {socketStatus}");
                break;
            default:
                Logger.Debug($"BiampTesira.TesiraComsManager.ClientStatusChangedHandler(): {socketStatus}");
                break;
        }

        ConnectionChanged?.Invoke(this, EventArgs.Empty);

        // TODO: BiampTesiraDsp.TesiraComsManager.ClientStatusChangedHandler() - start polling timer.
        Logger.Info("TODO: BiampTesiraDsp.TesiraComsManager.ClientStatusChangedHandler()");
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _client?.DisconnectFromServer();
            _client?.Dispose();
        }

        _disposed = true;
    }

    private string CreateString(byte[] bytes)
    {
        try
        {
            var chArray = new char[bytes.Length];
            for (var index = 0; index < bytes.Length; ++index)
                chArray[index] = (char)bytes[index];
            return new string(chArray);
        }
        catch (Exception e)
        {
            Logger.Error(
                $"BiampTesira.TesiraComsManager.ClientStatusChangedHandler(): {e.Message} -  {e.StackTrace?.Replace("\n", "\n\r")}");
            return string.Empty;
        }
    }

    #endregion private methods
}