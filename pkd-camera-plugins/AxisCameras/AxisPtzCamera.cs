using System.Collections.ObjectModel;
using AxisCameras.Http;
using pkd_common_utils.DataObjects;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_common_utils.Validation;
using pkd_hardware_service.BaseDevice;
using pkd_hardware_service.CameraDevices;
using pkd_hardware_service.PowerControl;

namespace AxisCameras;

public class AxisPtzCamera : BaseDevice, ICameraDevice, IDisposable, IPanTiltDevice, IZoomDevice, IPresetDevice
{
    private const string InfoQuery = "/axis-cgi/com/ptz.cgi?info=1&camera=1";
    private const string PanTilt = "/axis-cgi/com/ptz.cgi?camera=1&continuouspantiltmove=";
    private const string Zoom = "/axis-cgi/com/ptz.cgi?camera=1&continuouszoommove=";
    private const string PresetSave = "/axis-cgi/com/ptz.cgi?camera=1&setserverpresetno=";
    private const string PresetRecall = "/axis-cgi/com/ptz.cgi?camera=1&gotoserverpresetno=";
    
    private readonly AsyncHttpClient _client = new();
    private readonly Dictionary<string, CameraPreset> _presets = [];
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _disposed;

    ~AxisPtzCamera() => Dispose(false);

    public override void Connect()
    {
        if (!CheckInit(nameof(Connect))) return;
        Task.Run(() => _client.SendGetAsync(InfoQuery, _username, _password));
    }
    
    public bool SupportsSavingPresets => true;
    
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

    public void SetPresetData(List<CameraPreset> presets)
    {
        Logger.Debug("AxisPtzCamera.SetPresetData()");
        
        _presets.Clear();
        foreach (var preset in presets)
        {
            _presets.Add(preset.Id, preset);
        }
    }
    
    public ReadOnlyCollection<CameraPreset> QueryAllPresets()
    {
        return new ReadOnlyCollection<CameraPreset>(_presets.Values.ToList());
    }

    public void RecallPreset(string id)
    {
        if (!CheckInit(nameof(PresetRecall))) return;
        if (!_presets.TryGetValue(id, out var found))
        {
            Logger.Error($"AxisPtzCamera {Id} - RecallPreset() - no preset with id {id} found.");
            return;
        }

        Logger.Debug($"AxisPtzCamera {Id} - RecallPreset() - found =  {found.Id} - {found.Number}");
        var cmd = PresetRecall + found.Number;
        Task.Run(() => _client.SendGetAsync(cmd, _username, _password) );
    }

    public void SavePreset(string id)
    {
        if (!CheckInit(nameof(SavePreset))) return;
        if (!_presets.TryGetValue(id, out var found))
        {
            Logger.Error($"AxisPtzCamera {Id} - SavePreset() - no preset with id {id} found.");
            return;
        }

        var cmd = PresetSave + found.Number;
        Task.Run(() => _client.SendGetAsync(cmd, _username, _password) );
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