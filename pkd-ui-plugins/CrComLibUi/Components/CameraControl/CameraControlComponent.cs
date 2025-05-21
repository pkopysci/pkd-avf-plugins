namespace CrComLibUi.Components.CameraControl;

using Api;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json.Linq;
using pkd_application_service.CameraControl;
using pkd_application_service.UserInterface;
using pkd_common_utils.DataObjects;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;

internal class CameraControlComponent(
    BasicTriListWithSmartObject ui,
    UserInterfaceDataContainer uiData,
    ICameraControlApp appService)
    : BaseComponent(ui, uiData), IDisposable
{

    private const string ConfigCommand = "CONFIG";
    private const string StateCommand = "STATE";
    private const string PtzCommand = "PTZ";
    private const string ZoomCommand = "ZOOM";
    private const string PresetSaveCommand = "SAVE";
    private const string PresetRecallCommand = "RECALL";

    private List<CameraInfoContainer> _cameras = [];
    private bool _disposed;

    ~CameraControlComponent()
    {
        Dispose(false);
    }
    
    public override void HandleSerialResponse(string response)
    {
        var rxObj = MessageFactory.DeserializeMessage(response);
        var method = rxObj.Method.ToUpper();
        switch (method)
        {
            case "GET":
                HandleGetRequest(rxObj);
                break;
            case "POST":
                HandlePostRequest(rxObj);
                break;
            default:
                SendError($"HTTP Method {method} is not supported.", ApiHooks.Camera);
                break;
        }
    }

    public override void Initialize()
    {
        GetHandlers.Clear();
        PostHandlers.Clear();
        Initialized = false;

        _cameras = appService.GetAllCameraDeviceInfo().ToList();
        
        GetHandlers.Add(ConfigCommand, HandleGetConfig);
        GetHandlers.Add(StateCommand, HandleGetStateRequest);
        PostHandlers.Add(PtzCommand, HandlePostPtzRequest);
        PostHandlers.Add(ZoomCommand, HandlePostZoomRequest);
        PostHandlers.Add(PresetSaveCommand, HandlePostPresetSaveRequest);
        PostHandlers.Add(PresetRecallCommand, HandlePostPresetRecallRequest);
        
        appService.CameraControlConnectionChanged += AppServiceOnCameraControlConnectionChanged;
        appService.CameraPowerStateChanged += AppServiceOnCameraPowerStateChanged;
        
        Initialized = true;
    }

    private void AppServiceOnCameraPowerStateChanged(object? sender, GenericSingleEventArgs<string> e)
    {
        var id = e.Arg;
        var found = _cameras.FirstOrDefault(c => c.Id == id);
        if (found == null)
        {
            Logger.Error($"CameraControlComponent.SetCameraPowerState() - no device with id {id} found.");
            return;
        }
        
        found.PowerState = appService.QueryCameraPowerStatus(id);
        var rx = MessageFactory.CreateGetResponseObject();
        rx.Command = StateCommand;
        rx.Data["Camera"] = JToken.FromObject(found);
        Send(rx, ApiHooks.Camera);
    }

    private void AppServiceOnCameraControlConnectionChanged(object? sender, GenericSingleEventArgs<string> e)
    {
        var id = e.Arg;
        var found = _cameras.FirstOrDefault(c => c.Id == id);
        if (found == null)
        {
            Logger.Error($"CameraControlComponent.SetCameraConnectionStatus() - no device matching id {id}.");
            return;
        }
        
        found.IsOnline = appService.QueryCameraConnectionStatus(id);
        var rx = MessageFactory.CreateGetResponseObject();
        rx.Command = StateCommand;
        rx.Data["Camera"] = JToken.FromObject(found);
        Send(rx, ApiHooks.Camera);
    }

    public override void SendConfig()
    {
        HandleGetConfig(MessageFactory.CreateGetResponseObject());
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
            appService.CameraControlConnectionChanged -= AppServiceOnCameraControlConnectionChanged;
            appService.CameraPowerStateChanged -= AppServiceOnCameraPowerStateChanged;
        }
        
        _disposed = true;
    }
    
    private void HandleGetRequest(ResponseBase response)
    {
        if (GetHandlers.TryGetValue(response.Command, out var handler))
        {
            handler.Invoke(response);
        }
        else
        {
            SendError($"Unsupported GET Command: {response.Command}", ApiHooks.Camera);
        }
    }

    private void HandlePostRequest(ResponseBase response)
    {
        if (PostHandlers.TryGetValue(response.Command, out var handler))
        {
            handler.Invoke(response);
        }
        else
        {
            SendError($"Unsupported POST Command: {response.Command}", ApiHooks.Camera);
        }
    }

    private void HandleGetConfig(ResponseBase response)
    {
        response.Command = ConfigCommand;
        response.Data["Cameras"] = JToken.FromObject(_cameras);
        Send(response, ApiHooks.Camera);
    }

    private void HandleGetStateRequest(ResponseBase response)
    {
        var deviceId = response.Data.Value<string>("DeviceId");
        if (string.IsNullOrEmpty(deviceId))
        {
            SendError("Invalid GET state request: Missing DeviceId.", ApiHooks.Camera);
            return;
        }
        
        var device = _cameras.FirstOrDefault(c => c.Id == deviceId);
        if (device == null)
        {
            SendError($"Invalid GET state request: Device {deviceId} not found.", ApiHooks.Camera);
            return;
        }

        var rx = MessageFactory.CreatePostResponseObject();
        rx.Command = StateCommand;
        rx.Data["Camera"] = JToken.FromObject(device);
    }

    private void HandlePostPtzRequest(ResponseBase response)
    {
        var deviceId = response.Data.Value<string>("DeviceId");
        var xVal = response.Data.Value<float>("X");
        var yVal = response.Data.Value<float>("Y");
        if (string.IsNullOrEmpty(deviceId))
        {
            SendError("Invalid POST PTZ request: missing DeviceId or Direction.", ApiHooks.Camera);
            return;
        }
        
        var found = _cameras.FirstOrDefault(c => c.Id == deviceId);
        if (found == null)
        {
            SendError($"Invalid POST PTZ request: no device with id {deviceId} found.", ApiHooks.Camera);
            return;
        }
        
        if (!found.SupportsPanTilt)
        {
            SendError($"Invalid POST preset recall request. device {deviceId} does not support Pan/tilt.", ApiHooks.Camera);
            return;
        }
        
        var vector = new Vector2D() { X = xVal, Y = yVal };
        appService.SendCameraPanTilt(deviceId, vector);
    }

    private void HandlePostZoomRequest(ResponseBase response)
    {
        try
        {
            var deviceId = response.Data.Value<string>("DeviceId") ?? string.Empty;
            var speed = response.Data.Value<int>("Speed");
            var found = _cameras.FirstOrDefault(c => c.Id == deviceId);
            if (found == null)
            {
                SendError($"Invalid POST zoom request: no device with id {deviceId} found.", ApiHooks.Camera);
                return;
            }

            if (!found.SupportsZoom)
            {
                SendError($"Invalid POST preset recall request. device {deviceId} does not support zoom.", ApiHooks.Camera);
                return;
            }

            appService.SendCameraZoom(deviceId, speed);
        }
        catch (Exception)
        {
            SendError("Invalid POST zoom request: missing DeviceId or Speed.", ApiHooks.Camera);
        }
    }

    private void HandlePostPresetRecallRequest(ResponseBase response)
    {
        var deviceId = response.Data.Value<string>("DeviceId");
        var presetId = response.Data.Value<string>("PresetId");

        if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(presetId))
        {
            SendError($"Invalid POST preset recall request: missing DeviceId or PresetId.", ApiHooks.Camera);
            return;
        }
        
        var found = _cameras.FirstOrDefault(c => c.Id == deviceId);
        if (found == null)
        {
            SendError($"Invalid POST preset recall request: no device with id {deviceId} found.", ApiHooks.Camera);
            return;
        }

        if (found.Presets.FindIndex(x => x.Id == presetId) < 0)
        {
            SendError($"Invalid POST preset recall request: {deviceId} does not have a preset with id {presetId}.", ApiHooks.Camera);
            return;
        }

        appService.SendCameraPresetRecall(deviceId, presetId);
    }

    private void HandlePostPresetSaveRequest(ResponseBase response)
    {
        var deviceId = response.Data.Value<string>("DeviceId");
        var presetId = response.Data.Value<string>("PresetId");

        if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(presetId))
        {
            SendError($"Invalid POST preset recall request: missing DeviceId or PresetId.", ApiHooks.Camera);
            return;
        }
        
        var found = _cameras.FirstOrDefault(c => c.Id == deviceId);
        if (found == null)
        {
            SendError($"Invalid POST preset recall request: no device with id {deviceId} found.", ApiHooks.Camera);
            return;
        }

        if (!found.SupportsSavingPresets)
        {
            SendError($"Invalid POST preset recall request. device {deviceId} does not support saving presets.", ApiHooks.Camera);
            return;
        }

        appService.SendCameraPresetSave(deviceId, presetId);
    }
}