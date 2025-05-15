using System.Collections.ObjectModel;
using CrComLibUi.Api;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json.Linq;
using pkd_application_service.UserInterface;
using pkd_application_service.VideoWallControl;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;

namespace CrComLibUi.Components.VideoWallControl;

internal class VideoWallComponent(
    BasicTriListWithSmartObject ui,
    UserInterfaceDataContainer uiData)
    : BaseComponent(ui, uiData), IVideoWallUserInterface
{
    private const string LayoutCommand = "LAYOUT";
    private const string RouteCommand = "ROUTE";
    private const string StatusCommand = "STATUS";
    private const string ConfigCommand = "CONFIG";
    private List<VideoWallControlData> _videoWalls = [];
    
    public event EventHandler<GenericTrippleEventArgs<string, string, string>>? VideoWallLayoutChangeRequest;
    public event EventHandler<GenericTrippleEventArgs<string, string, string>>? VideoWallRouteRequest;

    public override void Initialize()
    {
        GetHandlers.Add(ConfigCommand, HandleGetConfig);
        GetHandlers.Add(LayoutCommand, HandleGetActiveLayout);
        PostHandlers.Add(LayoutCommand, HandlePostLayoutSelect);
        PostHandlers.Add(RouteCommand, HandlePostRouteRequest);
    }

    public override void HandleSerialResponse(string response)
    {
        try
        {
            var message = MessageFactory.DeserializeMessage(response);
            if (string.IsNullOrEmpty(message.Command))
            {
                SendError("Invalid message format.", ApiHooks.RoomConfig);
                return;
            }

            switch (message.Method)
            {
                case "GET":
                    HandleGetRequest(message);
                    break;
                case "POST":
                    HandlePostRequest(message);
                    break;
                default:
                    SendError($"Unsupported method: {message.Method}.",ApiHooks.VideoControl);
                    break;
            }
        }
        catch (Exception ex)
        {
            var errMessage = MessageFactory.CreateErrorResponse($"Invalid message format: {ex.Message}");
            Send(errMessage, ApiHooks.VideoControl);
        }
    }

    public void SetVideoWallData(
        ReadOnlyCollection<VideoWallInfoContainer> videoWalls)
    {
        _videoWalls = ConfigDataFactory.CreateControllerCollection(videoWalls);
    }

    public void UpdateActiveVideoWallLayout(string controlId,string canvasId, string layoutId)
    {
        var found = _videoWalls.FirstOrDefault(x => x.Id == controlId);
        if (found == null)
        {
            Logger.Error($"VideoWallComponent.UpdateActiveVideoWallLayout() - no controller with id {controlId}");
            return;
        }

        var canvas = found.Canvases.FirstOrDefault(c => c.Id.Equals(canvasId));
        if (canvas == null)
        {
            Logger.Error($"VideoWallComponent.UpdateActiveVideoWallLayout() - no canvas with id {canvasId}");
            return;
        }
        
        canvas.ActiveLayoutId  = layoutId;
        
        var message = MessageFactory.CreateGetResponseObject();
        message.Command = LayoutCommand;
        message.Data["ControlId"] = controlId;
        message.Data["CanvasId"] = canvasId;
        message.Data["LayoutId"] = layoutId;
        Send(message, ApiHooks.VideoWall);
    }

    public void UpdateCellRoutedSource(string controlId, string canvasId, string cellId, string sourceId)
    {
        var wall = _videoWalls.FirstOrDefault(x => x.Id == controlId);
        if (wall == null)
        {
            Logger.Error($"VideoWallComponent.UpdateCellRoutedSource() - no controller with id {controlId}");
            return;
        }
        
        var canvas = wall.Canvases.FirstOrDefault(c => c.Id.Equals(canvasId));
        if (canvas == null)
        {
            Logger.Error($"VideoWallComponent.UpdateCellRoutedSource() - no canvas with id {canvasId}");
            return;
        }

        var layout = canvas.GetActiveLayout();
        if (layout == null) return;
        layout.SetCellSource(cellId, sourceId);
        
        var message = MessageFactory.CreateGetResponseObject();
        message.Command = RouteCommand;
        message.Data["ControlId"] = controlId;
        message.Data["CanvasId"] = canvasId;
        message.Data["CellId"] = cellId;
        message.Data["SourceId"] = sourceId;
        Send(message, ApiHooks.VideoWall);
    }

    public void UpdateVideoWallConnectionStatus(string controlId, bool onlineStatus)
    {
        var found = _videoWalls.FirstOrDefault(x => x.Id == controlId);
        if (found == null)
        {
            Logger.Error($"VideoWallComponent.UpdateVideoWallConnectionStatus() - no controller with Id {controlId}");
            return;
        }

        found.IsOnline = onlineStatus;
        var message = MessageFactory.CreateGetResponseObject();
        message.Command = StatusCommand;
        message.Data["ControlId"] = controlId;
        message.Data["OnlineStatus"] = onlineStatus;
        Send(message, ApiHooks.VideoWall);
    }

    private void HandleGetRequest(ResponseBase rxObj)
    {
        if (GetHandlers.TryGetValue(rxObj.Command, out var handler))
        {
            handler.Invoke(rxObj);
        }
        else
        {
            SendError($"Unsupported GET command: {rxObj.Command}", ApiHooks.VideoWall);
        }
    }

    private void HandlePostRequest(ResponseBase rxObj)
    {
        if (PostHandlers.TryGetValue(rxObj.Command, out var handler))
        {
            handler.Invoke(rxObj);
        }
        else
        {
            SendError($"Unsupported POST command: {rxObj.Command}", ApiHooks.VideoWall);
        }
    }

    private void HandleGetConfig(ResponseBase rxObj)
    {
        var message = MessageFactory.CreateGetResponseObject();
        message.Command = ConfigCommand;
        message.Data["Controllers"] = JToken.FromObject(_videoWalls);
        Send(message, ApiHooks.VideoWall);
    }

    private void HandleGetActiveLayout(ResponseBase rxObj)
    {
        var controlId = rxObj.Data.Value<string>("ControlId");
        var canvasId = rxObj.Data.Value<string>("CanvasId");
        if (string.IsNullOrEmpty(controlId) ||  string.IsNullOrEmpty(canvasId))
        {
            SendError("Invalid Layout GET request - ControlId or CanvasId missing.", ApiHooks.VideoWall);
            return;
        }
        
        var controller = _videoWalls.FirstOrDefault(x => x.Id == controlId);
        if (controller == null)
        {
            SendError($"Invalid layout GET request - no control with id {controlId}", ApiHooks.VideoWall);
            return;
        }

        var canvas = controller.Canvases.FirstOrDefault(c => c.Id.Equals(canvasId));
        if (canvas == null)
        {
            SendError($"Invalid layout GET request - no canvas with id {canvasId}", ApiHooks.VideoWall);
            return;
        }
        
        var message = MessageFactory.CreateGetResponseObject();
        message.Command = LayoutCommand;
        message.Data["ControlId"] = controlId;
        message.Data["LayoutId"] = canvas.ActiveLayoutId;
        Send(message, ApiHooks.VideoWall);
    }
    
    private void HandlePostLayoutSelect(ResponseBase rxObj)
    {
        var controlId = rxObj.Data.Value<string>("ControlId");
        var canvasId = rxObj.Data.Value<string>("CanvasId");
        var layoutId = rxObj.Data.Value<string>("LayoutId");
        if (string.IsNullOrEmpty(controlId) || string.IsNullOrEmpty(layoutId) || string.IsNullOrEmpty(canvasId))
        {
            SendError("Invalid layout POST request: missing ControlId, canvasId, or LayoutId.", ApiHooks.VideoWall);
            return;
        }

        var wall = _videoWalls.FirstOrDefault(x => x.Id == controlId);
        if (wall == null)
        {
            SendError($"Invalid layout POST request: no controller with id {controlId}.", ApiHooks.VideoWall);
            return;
        }

        var canvas = wall.Canvases.FirstOrDefault(x => x.Id == canvasId);
        if (canvas == null)
        {
            SendError(
                $"Invalid layout POST request: controller {controlId} does not contain a canvas with id {canvasId}.",
                ApiHooks.VideoWall);
            return;
        }
        
        if (!canvas.Layouts.Exists(x => x.Id == canvasId))
        {
            SendError(
                $"Invalid layout POST request: canvas {canvasId} does not contain a canvas with id {layoutId}.",
                ApiHooks.VideoWall);
            return;
        }
        
        var temp = VideoWallLayoutChangeRequest;
        temp?.Invoke(this, new GenericTrippleEventArgs<string, string, string>(controlId, canvasId, layoutId));
    }

    private void HandlePostRouteRequest(ResponseBase rxObj)
    {
        var controlId = rxObj.Data.Value<string>("ControlId");
        var cellId = rxObj.Data.Value<string>("CellId");
        var sourceId = rxObj.Data.Value<string>("SourceId");
        if (string.IsNullOrEmpty(controlId) || string.IsNullOrEmpty(cellId) || string.IsNullOrEmpty(sourceId))
        {
            SendError("Invalid route POST request: missing ControlId, CellId, or SourceId.", ApiHooks.VideoWall);
            return;
        }
        
        var wall = _videoWalls.FirstOrDefault(x => x.Id == controlId);
        if (wall == null)
        {
            SendError($"Invalid route POST request: no controller with id {controlId}.", ApiHooks.VideoWall);
            return;
        }
        
        // todo
    }
}