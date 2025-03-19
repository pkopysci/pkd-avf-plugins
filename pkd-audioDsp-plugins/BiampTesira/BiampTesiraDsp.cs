using pkd_common_utils.GenericEventArgs;
using pkd_hardware_service.AudioDevices;
using pkd_hardware_service.BaseDevice;
using pkd_hardware_service.Routable;
using System.Timers;
using Crestron.SimplSharp.CrestronSockets;
using pkd_common_utils.Logging;
using pkd_common_utils.NetComs;


namespace BiampTesira;

public class BiampTesiraDsp : BaseDevice, IAudioRoutable, IDsp
{
    private const int RxTimeoutLength = 5000;
    private const int PollTime = 60000;
    private bool _disposed;
    private bool _sending;
    private BasicTcpClient? _client;
    private System.Timers.Timer _rxTimer;

    public BiampTesiraDsp()
    {
        _rxTimer = new System.Timers.Timer(RxTimeoutLength);
        _rxTimer.Elapsed += RxTimerOnElapsed;
    }

    public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputLevelChanged;
    public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputMuteChanged;
    public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputLevelChanged;
    public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputMuteChanged;
    public event EventHandler<GenericDualEventArgs<string, string>>? AudioRouteChanged;
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    public void Initialize(
        string hostId,
        int coreId,
        string hostname,
        int port,
        string username,
        string password)
    {
        IsInitialized = false;
        Id = hostId;
        if (_client != null)
        {
            UnsubscribeClient();
            _client?.Dispose();
            IsOnline = false;
        }

        _client = new BasicTcpClient(hostname, port);
        SubscribeClient();
        IsInitialized = true;
    }

    public override void Connect()
    {
        if (!IsInitialized)
        {
            Logger.Error($"BiampTesiraDsp.Connect() - Must call Initialize() before calling Connect().");
            return;
        }

        if (_client is not { Connected: false }) return;
        _client.Connect();
    }

    public override void Disconnect()
    {
        if (!IsInitialized)
        {
            Logger.Error($"BiampTesiraDsp.Disconnect() - Must call Initialize() before calling Disconnect().");
            return;
        }

        if (_client is not { Connected: true }) return;
        _client.Disconnect();
    }

    public IEnumerable<string> GetAudioPresetIds()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<string> GetAudioInputIds()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<string> GetAudioOutputIds()
    {
        throw new NotImplementedException();
    }

    public void SetAudioInputLevel(string id, int level)
    {
        throw new NotImplementedException();
    }

    public int GetAudioInputLevel(string id)
    {
        throw new NotImplementedException();
    }

    public void SetAudioInputMute(string id, bool mute)
    {
        throw new NotImplementedException();
    }

    public bool GetAudioInputMute(string id)
    {
        throw new NotImplementedException();
    }

    public void SetAudioOutputLevel(string id, int level)
    {
        throw new NotImplementedException();
    }

    public int GetAudioOutputLevel(string id)
    {
        throw new NotImplementedException();
    }

    public void SetAudioOutputMute(string id, bool mute)
    {
        throw new NotImplementedException();
    }

    public bool GetAudioOutputMute(string id)
    {
        throw new NotImplementedException();
    }

    public void AddPreset(string id, int index)
    {
        throw new NotImplementedException();
    }
    
    public void RecallAudioPreset(string id)
    {
        throw new NotImplementedException();
    }

    public void AddInputChannel(string id, string levelTag, string muteTag, int bankIndex, int levelMax, int levelMin,
        int routerIndex, List<string> tags)
    {
        throw new NotImplementedException();
    }

    public void AddOutputChannel(string id, string levelTag, string muteTag, string routerTag, int routerIndex, int bankIndex,
        int levelMax, int levelMin, List<string> tags)
    {
        throw new NotImplementedException();
    }
    
    public string GetCurrentAudioSource(string outputId)
    {
        throw new NotImplementedException();
    }

    public void RouteAudio(string sourceId, string outputId)
    {
        throw new NotImplementedException();
    }

    public void ClearAudioRoute(string outputId)
    {
        throw new NotImplementedException();
    }
    
    private void Dispose(bool disposing)
    {
        if (!_disposed) return;
        if (disposing)
        {
            // TODO: Release resources.
            _rxTimer.Elapsed -= RxTimerOnElapsed;
            _rxTimer?.Dispose();
            
            UnsubscribeClient();
            _client?.Dispose();
        }
        _disposed = true;
    }

    private void TrySend()
    {
        if (_sending || _client is not { Connected: true }) return;
        Logger.Debug($"BiampTesiraDsp {Id} - TrySend()");
    }
    
    private void RxTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        Logger.Error($"BiampTesiraDsp {Id} - no response from device.");
        _sending = false;
        TrySend();
    }

    private void SubscribeClient()
    {
        if (_client == null) return;
        _client.ClientConnected += ClientConnectedHandler;
        _client.StatusChanged += ClientStatusChangedHandler;
        _client.ConnectionFailed += ClientConnectionFailedHandler; 
        _client.RxReceived += ClientOnRxReceived;
    }

    private void UnsubscribeClient()
    {
        if (_client == null) return;
        _client.ClientConnected -= ClientConnectedHandler;
        _client.StatusChanged -= ClientStatusChangedHandler;
        _client.ConnectionFailed -= ClientConnectionFailedHandler; 
        _client.RxReceived -= ClientOnRxReceived;
    }
    
    private void ClientOnRxReceived(object? sender, GenericSingleEventArgs<string> e)
    {
        //TODO: BiampTesiraDsp.ClientOnRxReceived()
        Logger.Info("TODO: BiampTesiraDsp.ClientOnRxReceived()");
        Logger.Info(e.Arg);
    }

    private void ClientConnectionFailedHandler(object? sender, GenericSingleEventArgs<SocketStatus> e)
    {
        Logger.Error($"BiampTesiraDsp {Id} - Failed to connect to device: {e.Arg}");
        IsOnline = _client?.Connected ?? false;
        NotifyOnlineStatus();
    }

    private void ClientStatusChangedHandler(object? sender, EventArgs e) => ClientConnectedHandler(sender, e);

    private void ClientConnectedHandler(object? sender, EventArgs e)
    {
        IsOnline = _client?.Connected ?? false;
        NotifyOnlineStatus();
        // TODO: BiampTesiraDsp.ClientConnectedHandler() - start polling timer.
        Logger.Info("TODO: BiampTesiraDsp.ClientConnectedHandler() - start polling timer.");
    }
}