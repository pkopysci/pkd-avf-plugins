using System.Collections.ObjectModel;
using CrComLibUi.Api;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json.Linq;
using pkd_application_service.AvRouting;
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
    private List<AvSourceInfoContainer> _sources = [];
    
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
        ReadOnlyCollection<VideoWallInfoContainer> videoWalls,
        ReadOnlyCollection<AvSourceInfoContainer> sources)
    {
        _videoWalls = ConfigDataFactory.CreateControllerCollection(videoWalls);
        _sources = sources.ToList();
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
        message.Data["Sources"] = JToken.FromObject(_sources);
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

    }

    private void HandlePostRouteRequest(ResponseBase rxObj)
    {

    }
}