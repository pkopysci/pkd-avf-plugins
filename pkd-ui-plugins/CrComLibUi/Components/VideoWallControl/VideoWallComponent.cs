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
    private const string ConnectionCommand = "CONNECTION";
    private const string ConfigCommand = "CONFIG";
    private List<VideoWallControlData> _videoWalls = [];
    
    public event EventHandler<GenericDualEventArgs<string, string>>? VideoWallLayoutChangeRequest;
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

    public void UpdateActiveVideoWallLayout(string controlId, string layoutId)
    {
        var found = _videoWalls.FirstOrDefault(x => x.Id == controlId);
        if (found == null)
        {
            Logger.Error($"VideoWallComponent.UpdateActiveVideoWallLayout() - no controller with id {controlId}");
            return;
        }

        foreach (var layout in found.Layouts)
        {
            layout.IsActive = layout.Id == layoutId;
        }
        
        var message = MessageFactory.CreateGetResponseObject();
        message.Command = LayoutCommand;
        message.Data["ControlId"] = controlId;
        message.Data["LayoutId"] = layoutId;
        Send(message, ApiHooks.VideoWall);
    }

    public void UpdateCellRoutedSource(string controlId, string cellId, string sourceId)
    {
        var foundControl = _videoWalls.FirstOrDefault(x => x.Id == controlId);
        if (foundControl == null)
        {
            Logger.Error($"VideoWallComponent.UpdateCellRoutedSource() - no controller with id {controlId}");
            return;
        }
        
        var activeLayout = foundControl.Layouts.FirstOrDefault(x => x.IsActive);
        if (activeLayout == null)
        {
            Logger.Error($"VideoWallComponent.UpdateCellRoutedSource() - no layout is active");
            return;
        }
        
        var cell = activeLayout.Cells.FirstOrDefault(x => x.Id == cellId);
        if (cell == null)
        {
            Logger.Error($"VideoWallComponent.UpdateCellRoutedSource() - no cell found with id {cellId} on active layout.");
            return;
        }
        
        cell.SourceId = sourceId;
        
        var message = MessageFactory.CreateGetResponseObject();
        message.Command = RouteCommand;
        message.Data["ControlId"] = controlId;
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
        message.Command = ConnectionCommand;
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
        if (string.IsNullOrEmpty(controlId))
        {
            SendError("Invalid Layout GET request - ControlId missing.", ApiHooks.VideoWall);
            return;
        }
        
        var controller = _videoWalls.FirstOrDefault(x => x.Id == controlId);
        if (controller == null)
        {
            SendError($"Invalid layout GET request - no control with id {controlId}", ApiHooks.VideoWall);
            return;
        }
        
        var layout = controller.Layouts.FirstOrDefault( x => x.IsActive );
        var message = MessageFactory.CreateGetResponseObject();
        message.Command = LayoutCommand;
        message.Data["ControlId"] = controlId;
        message.Data["LayoutId"] = layout?.Id ?? string.Empty;
        Send(message, ApiHooks.VideoWall);
    }
    
    private void HandlePostLayoutSelect(ResponseBase rxObj)
    {
        var controlId = rxObj.Data.Value<string>("ControlId");
        var layoutId = rxObj.Data.Value<string>("LayoutId");
        if (string.IsNullOrEmpty(controlId) || string.IsNullOrEmpty(layoutId))
        {
            SendError("Invalid layout POST request: missing ControlId or LayoutId.", ApiHooks.VideoWall);
            return;
        }

        var wall = _videoWalls.FirstOrDefault(x => x.Id == controlId);
        if (wall == null)
        {
            SendError($"Invalid layout POST request: no controller with id {controlId}.", ApiHooks.VideoWall);
            return;
        }

        if (!wall.Layouts.Exists(x => x.Id == layoutId))
        {
            SendError(
                $"Invalid layout POST request: controller {controlId} does not contain a layout with id {layoutId}.",
                ApiHooks.VideoWall);
            return;
        }

        var temp = VideoWallLayoutChangeRequest;
        temp?.Invoke(this, new GenericDualEventArgs<string, string>(controlId, layoutId));
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
        
        var layout = wall.Layouts.FirstOrDefault(x => x.IsActive);
        if (layout == null)
        {
            SendError($"Invalid route POST request: Controller {controlId} has no active layout.", ApiHooks.VideoWall);
            return;
        }

        if (!layout.Cells.Exists(x => x.Id == cellId))
        {
            SendError($"Invalid route POST request: the active layout does not contain a cell with id {cellId}", ApiHooks.VideoWall);
            return;
        }

        if (!wall.Sources.Exists(x => x.Id == sourceId))
        {
            SendError($"Invalid route POSt request: no source exists with id {sourceId}", ApiHooks.VideoWall);
            return;
        }

        var temp = VideoWallRouteRequest;
        temp?.Invoke(this, new GenericTrippleEventArgs<string, string, string>(controlId, cellId, sourceId));
    }
}