using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_common_utils.NetComs;


namespace BiampTesira.Coms;

public partial class TesiraComsManager(string hostname, int port, string username, string password)
    : IDisposable
{
    private const string BoolRxPattern = """\+OK "value":(?<value>(true|false))""";
    private const string NumericRxPattern = """\+OK "value":(?<value>-*\d+\.*\d*)""";
    private const string SubRxPattern = """! "publishToken":"(?<subId>.*)" "value":(?<value>.*)""";
    
    [GeneratedRegex(BoolRxPattern)]
    private static partial Regex BoolRxRegex();
    
    [GeneratedRegex(NumericRxPattern)]
    private static partial Regex NumericRxRegex();
    
    private readonly Queue<TesiraComsData> _commandQueue = [];
    private readonly ListBuffer<byte> _rxBuffer = new();
    private readonly object _lock = new();
    private TCPClient? _client;
    private BasicTcpClient? _tcpClient = new(hostname, port);
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

    public bool Connected =>  _tcpClient is { Connected: true }; //_client is { ClientStatus : SocketStatus.SOCKET_STATUS_CONNECTED };
    public bool ReadyForCommands { get; private set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #region public methods

    public void Connect()
    {
        if (_tcpClient == null)//(_client == null)
        {
            _tcpClient = new BasicTcpClient(hostname, port);
            SubToClient();
            // _client = new TCPClient(hostname, port, 5000);
            // _client.SocketStatusChange += ClientStatusChangedHandler;
        }

        _tcpClient.Connect();
        //_client?.ConnectToServerAsync((_) => { });
    }
    
    public void Disconnect()
    {
        _tcpClient?.Disconnect();
        UnsubFromClient();
        _tcpClient?.Dispose();
        // _client?.DisconnectFromServer();
        // _client?.Dispose();
    }

    public void SendCommand(TesiraComsData commandData)
    {
        if (!Connected)
        {
            Logger.Error($"BiampTesira.TesiraComsManager {hostname}:{port} - EnqueueCommand() - client not connected.");
            return;
        }
        try
        {
            _commandQueue.Enqueue(commandData);
            Logger.Debug($"BiampTesira.TesiraComsManager.SendCommand() - {commandData.SerializedCommand}");
            var bytes = CreateBytes(commandData.SerializedCommand);
            //_client?.SendData(bytes, bytes.Length);
            _tcpClient?.Send(bytes);
        }
        catch (Exception e)
        {
            Logger.Error(
                $"BiampTesira.TesiraComsManager {hostname}:{port} - EnqueueCommand() exception: {e.Message} - {e.StackTrace?.Replace("\n", "\n\r")}");
        }
        
        // Task.Run(() =>
        // {
        //     lock (_lock)
        //     {
        //         try
        //         {
        //             _commandQueue.Enqueue(commandData);
        //             Logger.Debug($"BiampTesira.TesiraComsManager.SendCommand() - {commandData.SerializedCommand}");
        //             var bytes = CreateBytes(commandData.SerializedCommand);
        //             //_client?.SendData(bytes, bytes.Length);
        //             _tcpClient?.Send(bytes);
        //         }
        //         catch (Exception e)
        //         {
        //             Logger.Error(
        //                 $"BiampTesira.TesiraComsManager {hostname}:{port} - EnqueueCommand() exception: {e.Message} - {e.StackTrace?.Replace("\n", "\n\r")}");
        //         }
        //     }
        // });
    }

    #endregion

    #region private methods

    private void SubToClient()
    {
        if (_tcpClient == null) return;
        _tcpClient.ClientConnected += TcpClientOnClientConnected;
        _tcpClient.ConnectionFailed += TcpClientOnConnectionFailed;
        _tcpClient.StatusChanged += TcpClientOnStatusChanged;
        _tcpClient.RxBytesReceived += TcpClientOnRxBytesReceived;
    }

    private void UnsubFromClient()
    {
        if (_tcpClient == null) return;
        _tcpClient.ClientConnected -= TcpClientOnClientConnected;
        _tcpClient.ConnectionFailed += TcpClientOnConnectionFailed;
        _tcpClient.StatusChanged -= TcpClientOnStatusChanged;
        _tcpClient.RxBytesReceived -= TcpClientOnRxBytesReceived;
    }

    private void TcpClientOnClientConnected(object? sender, EventArgs e)
    {
        var temp = ConnectionChanged;
        temp?.Invoke(this, e);
    }
    private void TcpClientOnRxBytesReceived(object? sender, GenericSingleEventArgs<byte[]> e)
    {
        if (e.Arg.Length <= 0) return;
        var bytes = e.Arg;
        try
        {
            if (bytes[0] == 0xFF) // handshake request received
            {
                CrestronConsole.PrintLine("Handshake received:");
                foreach (var b in bytes)
                {
                    CrestronConsole.Print($"{b:X2} ");
                }

                CrestronConsole.PrintLine(string.Empty);

                var handshakeRx = GetHandshakeRx(bytes, bytes.Length);

                CrestronConsole.PrintLine("Sending response:");
                foreach (var b in handshakeRx)
                {
                    CrestronConsole.Print($"{b:X2} ");
                }

                CrestronConsole.PrintLine("\n\r");

                _tcpClient?.Send(handshakeRx);
                //_client?.SendDataAsync(handshakeRx, handshakeRx.Length, (_, _) => { });
            }
            else if (CreateString(bytes).Contains("Welcome to the Tesira Text Protocol Server..."))
            {
                ReadyForCommands = true;
                ReadyForCommandsReceived?.Invoke(this, EventArgs.Empty);
                // TODO: Handle prompts for login if authentication is enabled on the server.
            }
            else
            {
                CrestronConsole.PrintLine($"Rx Received: {CreateString(bytes)}");
                // _rxBuffer.AddItems(bytes);
                // HandlePostHandshakeResponse();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(
                $"BiampTesira.TesiraComsManager {hostname}:{port} - ClientOnRxReceived() - {ex.Message}:{ex.StackTrace?.Replace("\n", "\n\r")}");
        }
    }

    private void TcpClientOnStatusChanged(object? sender, EventArgs e)
    {
        CrestronConsole.PrintLine($"BasicTcpClient status change: {_tcpClient?.ClientStatusMessage}");
    }

    private void TcpClientOnConnectionFailed(object? sender, GenericSingleEventArgs<SocketStatus> e)
    {
        Logger.Error($"BiampTesira {hostname}:{port} - Failed to connect. Reason: {e.Arg}");
    }

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
                HandleOkResponse(command, rx);
            }
            else if (rx.Contains("-ERR"))
            {
                var command = _commandQueue.Dequeue();
                Logger.Error(
                    $"BiampTesira.TesiraComsManager {hostname}:{port} - Error RX received for {command.SerializedCommand} : {rx}");
            }
            else if (rx.Contains("! \"publishToken\":"))
            {
                HandleSubscriptionRx(rx);
            }
        }
    }

    private void HandleOkResponse(TesiraComsData data, string rx)
    {
        try
        {
            CrestronConsole.PrintLine($"Handle OK response: {rx}");
            // var match = BoolRxRegex().Match(rx);
            // if (match.Success)
            // {
            //     var value = bool.Parse(match.Groups["value"].Value);
            //     CrestronConsole.PrintLine($"Bool response for {data.SerializedCommand.Trim(['\r','\n', '\0'])} : {value}");
            // } 
            //
            // match = NumericRxRegex().Match(rx);
            // if (match.Success)
            // {
            //     var number = float.Parse(match.Groups["value"].Value);
            //     CrestronConsole.PrintLine($"Number response for  {data.SerializedCommand.Trim(['\r','\n', '\0'])} : {number}");
            // }
        }
        catch (Exception e)
        {
            Logger.Error($"BiampTesira.TesiraComsManager {hostname}:{port} - Failed to parse OK resposne() - {e.Message} : {e.StackTrace?.Replace("\n", "\n\r")}");
        }
    }
    
    private void HandleSubscriptionRx(string rx)
    {
        CrestronConsole.PrintLine($"Received Subscription response: {rx}");
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
            _tcpClient?.Disconnect();
            _tcpClient?.Dispose();
        }

        _disposed = true;
    }

    private static string CreateString(byte[] bytes)
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

    private static byte[] CreateBytes(string cmd)
    {
        var chars = cmd.ToCharArray();
        var bytes = new byte[chars.Length];
        for (var i = 0; i < chars.Length; i++)
        {
            bytes[i] = (byte)chars[i];
        }
        
        return bytes;
    }
    
    #endregion private methods
}