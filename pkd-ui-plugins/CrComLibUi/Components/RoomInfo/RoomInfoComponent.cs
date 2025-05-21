
namespace CrComLibUi.Components.RoomInfo;

using System;
using System.Collections.Generic;
using Api;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json.Linq;
using pkd_application_service;
using pkd_application_service.UserInterface;
using pkd_common_utils.Logging;
using Security;

internal class RoomInfoComponent(
    BasicTriListWithSmartObject ui,
    UserInterfaceDataContainer uiData,
    IApplicationService appService,
    SecurityComponent? securityUi) : BaseComponent(ui, uiData)
{
    private const string UseStateCommand = "USESTATE";
    private const string GetConfigCommand = "CONFIG";
    private bool _isSecure;
    private bool _isTech;
    private string _systemType = string.Empty;


    /// <inheritdoc/>
    public override void Initialize()
    {
        if (string.IsNullOrEmpty(_systemType))
        {
            Logger.Debug("CrComLibUi.RoomInfoComponent.Initialize() - SystemType has not been set.");
        }

        GetHandlers.Add(UseStateCommand, HandleRequestGetUseState);
        GetHandlers.Add(GetConfigCommand, HandleRequestGetConfig);
        PostHandlers.Add(UseStateCommand, HandleRequestPostUseState);
        
        _systemType = appService.GetRoomInfo().SystemType;
        _isSecure = UiData.Tags.FindIndex(x => x.Contains("secure")) > -1;
        _isTech = UiData.Tags.FindIndex(x => x.Contains("tech")) > -1;
        
        appService.SystemStateChanged += AppServiceOnSystemStateChanged;
        
        Initialized = true;
    }

    /// <inheritdoc/>
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
                    SendError($"Unsupported method: {message.Method}.", ApiHooks.RoomConfig);
                    break;
            }
        }
        catch (Exception ex)
        {
            var errMessage = MessageFactory.CreateErrorResponse($"Invalid message format: {ex.Message}");
            Send(errMessage, ApiHooks.RoomConfig);
        }
    }

    public override void SendConfig()
    {
        var rxObj = MessageFactory.CreateGetResponseObject();
        rxObj.Command = "CONFIG";
        HandleRequestGetConfig(rxObj);
    }

    private void AppServiceOnSystemStateChanged(object? sender, EventArgs e)
    {
        var state = appService.CurrentSystemState;
        JProperty newState = new("UseState", state);
        var command = MessageFactory.CreateGetResponseObject();
        command.Command = UseStateCommand;
        command.Data.Add(newState);
        Send(command, ApiHooks.RoomConfig);

        if (_isSecure && state)
        {
            securityUi?.EnableSecurityPasscodeLock();
        }
    }

    private void HandleGetRequest(ResponseBase rxObj)
    {
        if (GetHandlers.TryGetValue(rxObj.Command, out var handler))
        {
            handler.Invoke(rxObj);
        }
        else
        {
            SendError($"Unsupported GET command: {rxObj.Command}", ApiHooks.RoomConfig);
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
            SendError($"Unsupported POST command: {rxObj.Command}", ApiHooks.RoomConfig);
        }
    }

    private void HandleRequestGetConfig(ResponseBase rxObj)
    {
        List<MainMenuItem> menuItems = [];
        foreach (var item in UiData.MenuItems)
        {
            menuItems.Add(new MainMenuItem()
            {
                Id = item.Id,
                Label = item.Label,
                Icon = item.Icon,
                Control = item.Control,
                Source = item.SourceSelect,
                Tags = item.Tags,
            });
        }

        var config = new RoomConfigData()
        {
            Model = UiData.Model,
            IsInUse = appService.CurrentSystemState,
            RoomName = UiData.Label,
            HelpNumber = UiData.HelpContact,
            IsSecure = _isSecure,
            IsTech = _isTech,
            RoomType = _systemType,
            DefaultActivity = UiData.DefaultActivity,
            MainMenu = menuItems
        };

        var configResponse = MessageFactory.CreateGetResponseObject();
        configResponse.Command = GetConfigCommand;
        configResponse.Data = JObject.FromObject(config);
        Send(configResponse, ApiHooks.RoomConfig);
    }

    private void HandleRequestGetUseState(ResponseBase rxObj)
    {
        
        Logger.Debug($"{nameof(RoomInfoComponent)}.{nameof(HandleRequestGetConfig)}()");
        
        rxObj.Data.Add(new JProperty("UseState", appService.CurrentSystemState));
        Send(rxObj, ApiHooks.RoomConfig);
    }

    private void HandleRequestPostUseState(ResponseBase rxObj)
    {
        
        Logger.Debug($"{nameof(RoomInfoComponent)}.{nameof(HandleRequestPostUseState)}()");
        
        try
        {
            var state = rxObj.Data.Value<bool>("State");
            
            Logger.Debug($"{nameof(RoomInfoComponent)}.{nameof(HandleRequestPostUseState)}() - requested change: {state}");
            
            if (state == appService.CurrentSystemState) return;
            if (state)
            {
                appService.SetActive();
            }
            else
            {
                appService.SetStandby();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "CrComLibUi.RoomInfoComponent.HandleRequestPostUseState()");
            SendServerError(ApiHooks.RoomConfig);
        }
    }
}