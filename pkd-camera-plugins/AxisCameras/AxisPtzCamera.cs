using System.Collections.ObjectModel;
using AxisCameras.Http;
using pkd_common_utils.DataObjects;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_hardware_service.BaseDevice;
using pkd_hardware_service.CameraDevices;
using pkd_hardware_service.PowerControl;

namespace AxisCameras;

public class AxisPtzCamera : BaseDevice, ICameraDevice, IDisposable, IPanTiltDevice, IZoomDevice, IPresetDevice, IPowerControllable
{
    private const string InfoQuery = "/axis-cgi/com/ptz.cgi?info=1&camera=1";
    private const string PanTilt = "/axis-cgi/com/ptz.cgi?camera=1&continuouspantiltmove=";
    private const string Zoom = "/axis-cgi/com/ptz.cgi?camera=1&continuouszoommove=";
    private const string PresetSave = "/axis-cgi/com/ptz.cgi?camera=1&setserverpresetno=";
    private const string PresetRecall = "/axis-cgi/com/ptz.cgi?camera=1&gotoserverpresetno=";
    
    private readonly AsyncHttpClient _client = new();
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _disposed;

    ~AxisPtzCamera() => Dispose(false);

    public override void Connect()
    {
        if (!CheckInit(nameof(Connect))) return;
        Task.Run(() => _client.SendGetAsync(InfoQuery, _username, _password));
    }

    public event EventHandler<GenericSingleEventArgs<string>>? PowerChanged;
    
    public bool SupportsSavingPresets => true;

    public bool PowerState { get; private set; }
    
    public override void Disconnect()
    {
        if (!CheckInit(nameof(Disconnect)) || !IsOnline) return;
        _client.CancelPendingRequests();
        IsOnline = false;
        NotifyOnlineStatus();
    }

    public void Initialize(string hostname, int port, string id, string label, string username, string password)
    {
        IsInitialized = false;
        Id = id;
        Label = label;
        _client.BaseUrl = $"http://{hostname}";
        _client.RequestOkCallback = ResponseOkHandler;
        _client.RequestFailedCallback = RequestFailedHandler;
        _client.RequestTimeoutCallback = RequestTimeoutHandler;

        _username = username;
        _password = password;
        IsInitialized = true;
    }

    public void SetPanTilt(Vector2D direction)
    {
        if (!CheckInit(nameof(SetPanTilt))) return;
        var cmd = PanTilt + $"{direction.X},{direction.Y}";
        Task.Run(() => _client.SendGetAsync(cmd, _username, _password));
    }
    
    public void SetZoom(int speed)
    {
        if (!CheckInit(nameof(SetZoom))) return;
        var cmd =  Zoom + speed;
        Task.Run(() => _client.SendGetAsync(cmd, _username, _password));
    }
    
    public ReadOnlyCollection<CameraPreset> QueryAllPresets()
    {
        throw new NotImplementedException();
    }

    public void RecallPreset(string id)
    {
        throw new NotImplementedException();
    }

    public void SavePreset(string id)
    {
        throw new NotImplementedException();
    }

    public void PowerOn()
    {
        throw new NotImplementedException();
    }

    public void PowerOff()
    {
        throw new NotImplementedException();
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
            _client.Dispose();
        }
        _disposed = true;
    }
    
    private bool CheckInit(string methodName)
    {
        if (!IsInitialized)
        {
            Logger.Error($"AxisPtzCamera.{methodName}() - Call Initialize() first.");
        }

        return IsInitialized;
    }

    private Task ResponseOkHandler(AsyncHttpClient client)
    {
        Logger.Debug($"AxisPtzCamera {Id} -  ResponseOkHandler()");
        if (!IsOnline)
        {
            IsOnline = true;
            NotifyOnlineStatus();
        }
        
        return Task.CompletedTask;
    }

    private Task RequestFailedHandler(AsyncHttpClient client, string message)
    {
        Logger.Error($"AxisPtzCamera {Id} -  RequestFailedHandler() - {message}");
        if (IsOnline)
        {
            IsOnline = false;
            NotifyOnlineStatus();
        }
        
        return Task.CompletedTask;
    }

    private void RequestTimeoutHandler(AsyncHttpClient client, string message)
    {
        Logger.Error($"AxisPtzCamera {Id} -  RequestTimeoutHandler() - {message}");
        if (IsOnline)
        {
            IsOnline = false;
            NotifyOnlineStatus();
        }
    }
}