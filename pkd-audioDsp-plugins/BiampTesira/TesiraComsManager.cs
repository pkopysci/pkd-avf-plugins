using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using pkd_common_utils.Logging;

namespace BiampTesira;

public class TesiraComsManager(string hostname, int port, string username, string password)
    : IDisposable
{
    private readonly Queue<TesiraComsData> _commandQueue = [];
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

        _client?.ConnectToServerAsync(ClientConnectedHandler);
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

                    _client?.SendDataAsync(bytes, bytes.Length, (c, n) => { });
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
            Logger.Debug($"BiampTesira.TesiraComsManager {hostname}:{port} - ClientRxReceived()");
            try
            {
                var bytes = client.IncomingDataBuffer.Take(bytesReceived).ToArray();

                if (bytes[0] == 0xFF) // handshake request received
                {
                    var handshakeRx = GetHandshakeRx(bytes, bytesReceived);
                    _client?.SendDataAsync(handshakeRx, handshakeRx.Length,
                        (c, n) =>
                        {
                            Logger.Debug($"BiampTesira.TesiraComsManager {hostname}:{port} - sent handshake Rx");
                        });
                }
                else
                {
                    var chArray = new char[bytes.Length];
                    for (var index = 0; index < bytes.Length; ++index)
                        chArray[index] = (char)bytes[index];
                    HandlePostHandshakeResponse(new string(chArray));
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
        CrestronConsole.PrintLine("Telnet Handshake Received");
        var handshakeTx = new byte[bytesReceived];
        for (var i = 0; i < bytesReceived; i++)
        {
            handshakeTx[i] = HandshakeByteMap.TryGetValue(response[i], out var rxMapped) ? rxMapped : response[i];
        }

        return handshakeTx;
    }

    private void HandlePostHandshakeResponse(string rx)
    {
        // TODO: BiampTesiraDsp.TesiraComsManager.HandlePostHandshakeResponse()
        Logger.Debug($"BiampTesira.TesiraComsManager {hostname}:{port} - rx string: {rx}");
        if (rx.Contains("Welcome to the Tesira Text Protocol Server..."))
        {
            ReadyForCommands = true;
            ReadyForCommandsReceived?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_commandQueue.TryDequeue(out var command))
        {
            Logger.Debug($"BiampTesira.TesiraComsManager() - Dequeued Command: {command.SerializedCommand}");
        }
        else
        {
            Logger.Error($"BiampTesira.TesiraComsManager.ClientOnRxReceived() - could not remove command from queue.");
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
                break;
            case SocketStatus.SOCKET_STATUS_BROKEN_REMOTELY:
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

    private void ClientConnectedHandler(TCPClient client)
    {
        // TODO: BiampTesiraDsp.ClientConnectedHandler() - start polling timer.
        Logger.Info("TODO: BiampTesiraDsp.TesiraComsManager.ClientConnectedHandler()");
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

    #endregion private methods
}