using System.Collections.ObjectModel;
using CrComLibUi.Api;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json.Linq;
using pkd_application_service.CameraControl;
using pkd_application_service.UserInterface;
using pkd_common_utils.DataObjects;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;

namespace CrComLibUi.Components.CameraControl;

internal class CameraControlComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
    : BaseComponent(ui, uiData), ICameraUserInterface
{

    private const string ConfigCommand = "CONFIG";
    private const string StateCommand = "STATE";
    private const string PtzCommand = "PTZ";
    private const string ZoomCommand = "ZOOM";
    private const string PresetSaveCommand = "SAVE";
    private const string PresetRecallCommand = "RECALL";

    private List<CameraInfoContainer> _cameras = [];
    
    public event EventHandler<GenericDualEventArgs<string, Vector2D>>? CameraPanTiltRequest;
    public event EventHandler<GenericDualEventArgs<string, int>>? CameraZoomRequest;
    public event EventHandler<GenericDualEventArgs<string, string>>? CameraPresetRecallRequest;
    public event EventHandler<GenericDualEventArgs<string, string>>? CameraPresetSaveRequest;
    public event EventHandler<GenericDualEventArgs<string, bool>>? CameraPowerChangeRequest;
    
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
        GetHandlers.Add(ConfigCommand, HandleGetConfig);
        GetHandlers.Add(StateCommand, HandleGetStateRequest);
        PostHandlers.Add(PtzCommand, HandlePostPtzRequest);
        PostHandlers.Add(ZoomCommand, HandlePostZoomRequest);
        PostHandlers.Add(PresetSaveCommand, HandlePostPresetSaveRequest);
        PostHandlers.Add(PresetRecallCommand, HandlePostPresetRecallRequest);
        Initialized = true;
    }

    public void SetCameraData(ReadOnlyCollection<CameraInfoContainer> cameras)
    {
        _cameras = cameras.ToList();
    }

    public void SetCameraPowerState(string id, bool newState)
    {
        var found = _cameras.FirstOrDefault(c => c.Id == id);
        if (found == null)
        {
            Logger.Error($"CameraControlComponent.SetCameraPowerState({id}, {newState}) - no device with id found.");
            return;
        }
        
        found.PowerState = newState;
        var rx = MessageFactory.CreateGetResponseObject();
        rx.Command = StateCommand;
        rx.Data["Camera"] = JToken.FromObject(found);
        Send(rx, ApiHooks.Camera);
    }

    public void SetCameraConnectionStatus(string id, bool newState)
    {
        var found = _cameras.FirstOrDefault(c => c.Id == id);
        if (found == null)
        {
            Logger.Error($"CameraControlComponent.SetCameraConnectionStatus({id}, {newState}) - no device matching id.");
            return;
        }
        
        found.IsOnline = newState;
        var rx = MessageFactory.CreateGetResponseObject();
        rx.Command = StateCommand;
        rx.Data["Camera"] = JToken.FromObject(found);
        Send(rx, ApiHooks.Camera);
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
        var direction = response.Data.Value<Vector2D>("Direction");
        if (string.IsNullOrEmpty(deviceId) || direction == null)
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

        var temp = CameraPanTiltRequest;
        temp?.Invoke(this, new GenericDualEventArgs<string, Vector2D>(deviceId, direction));
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
            
            var temp = CameraZoomRequest;
            temp?.Invoke(this, new GenericDualEventArgs<string, int>(deviceId, speed));
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

        var temp = CameraPresetRecallRequest;
        temp?.Invoke(this, new GenericDualEventArgs<string, string>(deviceId, presetId));
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

        var temp = CameraPresetSaveRequest;
        temp?.Invoke(this, new GenericDualEventArgs<string, string>(deviceId, presetId));
    }
}