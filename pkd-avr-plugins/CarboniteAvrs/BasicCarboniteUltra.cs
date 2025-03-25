using Crestron.SimplSharp.CrestronSockets;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_common_utils.NetComs;
using pkd_hardware_service.AvSwitchDevices;
using pkd_hardware_service.BaseDevice;

namespace CarboniteAvrs;

/// <summary>
/// Basic implementation of matrix routing for a Carbonite Ultra production switcher. This plugin uses RossTalk to send
/// commands. This means all provided feedback is not returned from the device and is set when the route methods are called
/// (fake feedback). This is a very simple implementation and does not support all features of a Carbonite Ultra production switcher.
/// </summary>
/// <remarks>
/// | Output Index | Hardware Output |<br/>
/// | ------------ | --------------- |<br/>
/// | 1            | MV IO 1 - Box 1 |<br/>
/// | 2            | MV IO 1 - Box 2 |<br/>
/// | 3            | MV IO 1 - Box 3 |<br/>
/// | 4            | MV IO 1 - Box 4 |<br/>
/// | 5            | MV IO 2 - Box 1 |<br/>
/// | 6            | MV IO 2 - Box 2 |<br/>
/// | 7            | MV IO 2 - Box 3 |<br/>
/// | 8            | MV IO 2 - Box 4 |<br/>
/// | 9            | MV VP 1 - Box 1 |<br/>
/// | 10           | MV VP 1 - Box 2 |<br/>
/// | 11           | MV VP 1 - Box 3 |<br/>
/// | 12           | MV VP 1 - Box 4 |<br/>
/// | 13           | MV VP 2 - Box 1 |<br/>
/// | 14           | MV VP 2 - Box 2 |<br/>
/// | 15           | MV VP 2 - Box 3 |<br/>
/// | 16           | MV VP 2 - Box 4 |<br/>
/// | 17           | AUX Bus 1       |<br/>
/// | 18           | Aux Bus 2       |<br/>
/// | 19           | Aux Bus 3       |<br/>
/// | 20           | Aux Bus 4       |<br/>
/// | 21           | Aux Bus 5       |<br/>
/// | 22           | Aux Bus 6       |<br/>
/// | 23           | Aux Bus 7       |<br/>
/// | 24           | Aux Bus 8       |<br/>
///</remarks>
public class BasicCarboniteUltra : BaseDevice, IAvSwitcher, IDisposable
{
    private bool _disposed;
    private BasicTcpClient? _client;
    private string _hostname = string.Empty;
    private int _port;
    private int _numInputs;
    private uint[] _outputs = [];

    ~BasicCarboniteUltra()
    {
        Dispose(false);
    }

    public event EventHandler<GenericDualEventArgs<string, uint>>? VideoRouteChanged;

    public override bool IsOnline => _client is { Connected: true };

    public void Initialize(string hostName, int port, string id, string label, int numInputs = 24, int numOutputs = 24)
    {
        IsInitialized = false;
        _hostname = hostName;
        _port = port;
        Id = id;
        Label = label;
        _numInputs = numInputs;
        _outputs = new uint[numOutputs];
        IsInitialized = true;
    }

    public override void Connect()
    {
        if (_client?.Connected == true || !CheckInit(nameof(Connect))) return;
        if (_client == null)
        {
            Logger.Debug($"BasicCarboniteUltra {Id} - Creating new tcp client...");

            _client = new BasicTcpClient(_hostname, _port);
            _client.EnableReconnect = true;
            SubscribeClient();
        }

        Logger.Debug($"BasicCarboniteUltra {Id} - connecting...");

        _client.Connect();
    }

    public override void Disconnect()
    {
        if (_client is not { Connected: true } || !CheckInit(nameof(Disconnect))) return;

        Logger.Debug($"BasicCarboniteUltra {Id} - Disconnect()");

        _client.EnableReconnect = false;
        _client.Disconnect();
        UnsubscribeClient();
        _client.Dispose();
        _client = null;
    }

    public uint GetCurrentVideoSource(uint output)
    {
        if (_client is not { Connected: true } || !CheckInit(nameof(Disconnect))) return 0;
        if (output < 1 || output > _outputs.Length)
        {
            Logger.Error($"Carbonite Ultra {Id} - GetCurrentVideoSource() - {output} is out of bounds.");
            return 0;
        }

        return _outputs[output];
    }

    public void RouteVideo(uint source, uint output)
    {
        if (_client is not { Connected: true } || !CheckInit(nameof(Disconnect))) return;
        if (source > _numInputs)
        {
            Logger.Error($"Carbonite Ultra {Id} -  RouteVideo() - {source} is greater than the number of inputs.");
            return;
        }

        if (output < 1 || output > _outputs.Length)
        {
            Logger.Error($"Carbonite Ultra {Id} -  RouteVideo() - {output} is is out of bounds.");
            return;
        }

        switch (output)
        {
            case <= 8:
                SendRouteToIo(source, output);
                break;
            case <= 16:
                SendRouteToVp(source, output);
                break;
            default:
                SendRouteToBus(source, output);
                break;
        }
    }

    public void ClearVideoRoute(uint output)
    {
        if (_client is not { Connected: true } || !CheckInit(nameof(Disconnect))) return;
        if (output < 1 || output > _outputs.Length)
        {
            Logger.Error($"Carbonite Ultra {Id} -  ClearVideoRoute() - {output} is is out of bounds.");
            return;
        }

        switch (output)
        {
            case <= 8:
                SendRouteToIo(0, output);
                break;
            case <= 16:
                SendRouteToVp(0, output);
                break;
            default:
                SendRouteToBus(0, output);
                break;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _client?.Disconnect();
            UnsubscribeClient();
            _client?.Dispose();
        }

        _disposed = true;
    }

    private void SubscribeClient()
    {
        if (_client == null) return;
        _client.ClientConnected += ClientOnClientConnected;
        _client.StatusChanged += ClientOnStatusChanged;
        _client.ConnectionFailed += ClientOnConnectionFailed;
    }

    private void UnsubscribeClient()
    {
        if (_client == null) return;
        _client.ClientConnected -= ClientOnClientConnected;
        _client.StatusChanged -= ClientOnStatusChanged;
        _client.ConnectionFailed -= ClientOnConnectionFailed;
    }

    private void ClientOnClientConnected(object? sender, EventArgs e) => NotifyOnlineStatus();

    private void ClientOnConnectionFailed(object? sender, GenericSingleEventArgs<SocketStatus> e)
    {
        Logger.Error($"Carbonite Ultra {Id}:{_hostname} - Connection attempt failed. Reason: {e.Arg}");
        NotifyOnlineStatus();
    }

    private void ClientOnStatusChanged(object? sender, EventArgs e)
    {
        Logger.Debug(
            $"Carbonite Ultra {Id}:{_hostname} - Client status changed: {IsOnline}, reason: {_client?.ClientStatusMessage}");
        NotifyOnlineStatus();
    }

    private bool CheckInit(string methodName)
    {
        if (!IsInitialized)
        {
            Logger.Error($"CarboniteUltra.{methodName}() - Call Initialize() first.");
        }

        return IsInitialized;
    }

    private void SendRouteToIo(uint source, uint output)
    {
        var cmd = source == 0
            ? $"MVBOX IO:{(output > 4 ? 2 : 1)}:{(output > 4 ? output - 4 : output)}:BK\n\r"
            : $"MVBOX IO:{(output > 4 ? 2 : 1)}:{(output > 4 ? output - 4 : output)}:IN:{source}\n\r";

        Logger.Debug($"BasicCarboniteUltra {Id} - SendRouteToIo(): {cmd}");

        _outputs[output] = source;
        _client?.Send(cmd);
        var temp = VideoRouteChanged;
        temp?.Invoke(this, new GenericDualEventArgs<string, uint>(Id, output));
    }

    private void SendRouteToVp(uint source, uint output)
    {
        var vpNumber = output > 12 ? 2 : 1;
        var boxNumber = output > 12 ? output - 12 : output - 8;

        var cmd = source == 0
            ? $"MVBOX VP:{vpNumber}:{boxNumber}:BK\n\r"
            : $"MVBOX VP:{vpNumber}:{boxNumber}:IN:{source}\n\r";

        Logger.Debug($"BasicCarboniteUltra {Id} - SendRouteToVp(): {cmd}");

        _outputs[output] = source;
        _client?.Send(cmd);
        var temp = VideoRouteChanged;
        temp?.Invoke(this, new GenericDualEventArgs<string, uint>(Id, output));
    }

    private void SendRouteToBus(uint source, uint output)
    {
        var auxNumber = output - 16;
        var cmd = $"XPT AUX:{auxNumber}:IN:{source}\n\r";

        Logger.Debug($"BasicCarboniteUltra {Id} - SendRouteToBus(): {cmd}");

        _outputs[output] = source;
        _client?.Send(cmd);
        var temp = VideoRouteChanged;
        temp?.Invoke(this, new GenericDualEventArgs<string, uint>(Id, output));
    }
}