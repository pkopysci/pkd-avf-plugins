// ReSharper disable SuspiciousTypeConversion.Global


namespace CrComLibUi;

using Api;
using System;
using System.Collections.Generic;
using Components.AudioControl;
using Components.CameraControl;
using Components.CustomEvents;
using Components.ErrorReporting;
using Components.Lighting;
using Components.RoomInfo;
using Components.Security;
using Components.TransportControl;
using Components.VideoControl;
using Components.VideoWallControl;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.UI;
using pkd_application_service;
using pkd_application_service.AvRouting;
using pkd_application_service.CameraControl;
using pkd_application_service.CustomEvents;
using pkd_application_service.DisplayControl;
using pkd_application_service.LightingControl;
using pkd_application_service.TransportControl;
using pkd_application_service.UserInterface;
using pkd_application_service.VideoWallControl;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;

/// <summary>
/// Class for connecting to a Crestron TSW interface running a display project using a Json-based protocol via Crestron CrComLib protocol.
/// Once created, the following methods must be called to build the underlying configuration and listen for a connection:
/// SetUiData(), SetCrestronControl(), SetSystemType(), Initialize(), Connect()
/// </summary>
public class CrComLibUserInterface :
    IUserInterface,
    IUsesApplicationService,
    ICrestronUserInterface,
    IErrorInterface,
    IDisposable
{
    private readonly List<IVueUiComponent> _uiComponents = [];
    private readonly Dictionary<uint, Action<string>> _apiHandlerActions = [];
    private BasicTriListWithSmartObject? _ui;
    private CrestronControlSystem? _parent;
    private UserInterfaceDataContainer? _uiData;
    private IApplicationService? _appService;
    private uint _ipId;
    private bool _disposed;

    ~CrComLibUserInterface()
    {
        Dispose(false);
    }

    public event EventHandler<GenericSingleEventArgs<string>>? OnlineStatusChanged;

    /// <inheritdoc/>
    public bool IsInitialized { get; private set; }

    /// <inheritdoc/>
    public bool IsOnline { get; private set; }

    /// <inheritdoc/>
    public bool IsXpanel => false;

    /// <inheritdoc/>
    public string Id { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public void Connect()
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public void SetUiData(UserInterfaceDataContainer uiData)
    {
        _uiData = uiData;
        Id = uiData.Id;
    }

    /// <inheritdoc/>
    public void SetCrestronControl(CrestronControlSystem parent)
    {
        if (_uiData == null)
        {
            Logger.Error("CrComLibUi.CrComLibUserInterface.SetCrestronControl() - call SetUiData() first.");
            return;
        }

        _parent = parent;
        _ipId = (uint)_uiData.IpId;
        RebuildInterface();
        _uiComponents.Clear();
        CreateComponents();
    }

    /// <inheritdoc/>
    public void Initialize()
    {
        Logger.Debug($"CrComLibUi.CrComLibUserInterface.Initialize() - {Id}");

        IsInitialized = false;
        if (_uiData == null)
        {
            Logger.Error("CrComLibUserInterface.Initialize() - SetUiData() was not called.");
            return;
        }

        if (_ui?.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
        {
            Logger.Error(
                $"CrComLibUi.CrComLibUserInterface.Initialize() - failed to register UI: {_ui?.RegistrationFailureReason}");
            return;
        }

        foreach (var component in _uiComponents)
        {
            component.Initialize();
        }

        IsInitialized = true;
    }

    public void AddDeviceError(string id, string label)
    {
        FindComponent<IErrorInterface>()?.AddDeviceError(id, label);
    }

    public void ClearDeviceError(string id)
    {
        FindComponent<IErrorInterface>()?.ClearDeviceError(id);
    }

    public void SetApplicationService(IApplicationService applicationService)
    {
        _appService = applicationService;
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            foreach (var component in _uiComponents)
            {
                (component as IDisposable)?.Dispose();
            }

            _uiComponents.Clear();
            DisposeUi();
        }

        _disposed = true;
    }

    private void DisposeUi()
    {
        if (_ui == null) return;
        _ui.OnlineStatusChange -= UiOnOnlineStatusChange;
        _ui.SigChange -= UiOnSigChange;
        _ui.UnRegister();
        _ui.Dispose();
    }

    private void RebuildInterface()
    {
        DisposeUi();
        if (_uiData == null)
        {
            Logger.Error("CrComLibUi.CrComLibUserInterface.RebuildInterface() - UI data is null.");
            return;
        }

        switch (_uiData.Model.ToUpper())
        {
            case "TSW770":
            case "TSW-770":
            case "TSW770-HTML": // backwards compatability with old configs
                _ui = new Tsw770(_ipId, _parent);
                break;
            case "TSW1070":
            case "TSW-1070":
                _ui = new Tsw1070(_ipId, _parent);
                break;
            default:
                Logger.Error("CrComLibUi.CrComLibUserInterface.RebuildInterface() - Unsupported model: {0}",
                    _uiData.Model);
                return;
        }

        _ui.SigChange += UiOnSigChange;
        _ui.OnlineStatusChange += UiOnOnlineStatusChange;
    }

    private void UiOnSigChange(BasicTriList currentDevice, SigEventArgs args)
    {
        if (args.Sig.Type != eSigType.String) return;
        if (_apiHandlerActions.TryGetValue(args.Sig.Number, out var handler))
        {
            handler.Invoke(args.Sig.StringValue);
        }
    }

    private void UiOnOnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
    {
        IsOnline = args.DeviceOnLine;
        if (IsOnline)
        {
            foreach (var component in _uiComponents)
            {
                component.SendConfig();
            }
        }

        var temp = OnlineStatusChanged;
        temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
    }

    private void CreateComponents()
    {
        if (_ui == null || _uiData == null)
        {
            Logger.Error("CrComLibUi.CrComLibUserInterface.CreateComponents() - ui or uiData has not been set.");
            return;
        }

        if (_appService == null)
        {
            Logger.Error("CrComLibUi.CrComLibUserInterface.CreateComponents() - Call SetApplicationService() first.");
            return;
        }

        try
        {
            Logger.Debug("CrComLibUi.CrComLibUserInterface.CreateComponents()");
            
            SecurityComponent? securityComponent = null;
            if (_appService is ITechAuthGroupAppService techApp)
            {
                securityComponent = new SecurityComponent(_ui, _uiData, techApp);
                _apiHandlerActions.Add((uint)ApiHooks.Security, securityComponent.HandleSerialResponse);
            }

            var roomInfoComponent = new RoomInfoComponent(_ui, _uiData, _appService, securityComponent);
            _uiComponents.Add(roomInfoComponent);
            _apiHandlerActions.Add((uint)ApiHooks.RoomConfig, roomInfoComponent.HandleSerialResponse);

            var audioComponent = new AudioControlComponent(_ui, _uiData, _appService);
            _uiComponents.Add(audioComponent);
            _apiHandlerActions.Add((uint)ApiHooks.AudioControl, audioComponent.HandleSerialResponse);

            var errorComponent = new ErrorComponent(_ui, _uiData);
            _uiComponents.Add(errorComponent);
            _apiHandlerActions.Add((uint)ApiHooks.Errors, errorComponent.HandleSerialResponse);

            if (_appService is ICameraControlApp camApp)
            {
                var cameraComponent = new CameraControlComponent(_ui, _uiData, camApp);
                _uiComponents.Add(cameraComponent);
                _apiHandlerActions.Add((uint)ApiHooks.Camera, cameraComponent.HandleSerialResponse);
                                
            }

            if (_appService is ICustomEventAppService eventApp)
            {
                var customEventComponent = new CustomEventComponent(_ui, _uiData, eventApp);
                _uiComponents.Add(customEventComponent);
                _apiHandlerActions.Add((uint)ApiHooks.Event, customEventComponent.HandleSerialResponse);
                
            }

            if (_appService is ILightingControlApp lightingApp)
            {
                var lightingComponent = new LightingComponent(_ui, _uiData, lightingApp);
                _uiComponents.Add(lightingComponent);
                _apiHandlerActions.Add((uint)ApiHooks.LightingControl, lightingComponent.HandleSerialResponse);
                
            }

            if (_appService is ITransportControlApp transportApp)
            {
                var transportComponent = new TransportComponent(_ui, _uiData, transportApp);
                _uiComponents.Add(transportComponent);
                _apiHandlerActions.Add((uint)ApiHooks.DeviceControl, transportComponent.HandleSerialResponse);
            }

            if (_appService is IVideoWallApp videoWallApp)
            {
                var videoWallComponent = new VideoWallComponent(_ui, _uiData, videoWallApp);
                _uiComponents.Add(videoWallComponent);
                _apiHandlerActions.Add((uint)ApiHooks.VideoWall, videoWallComponent.HandleSerialResponse);
            }

            if (_appService is IAvRoutingApp routingApp)
            {
                var avComponent = new VideoControlComponent(_ui, _uiData, routingApp);
                _uiComponents.Add(avComponent);
                _apiHandlerActions.Add((uint)ApiHooks.VideoControl, avComponent.HandleSerialResponse);
                
            }

            if (_appService is IDisplayControlApp displayApp)
            {
                var displayComponent = new DisplayControlComponent(_ui, _uiData, displayApp);
                _uiComponents.Add(displayComponent);
                _apiHandlerActions.Add((uint)ApiHooks.DisplayChange, displayComponent.HandleSerialResponse);
                
            }
        }
        catch (Exception e)
        {
            Logger.Error("CrComLibUi.CrComLibUserInterface.CreateComponents() - Failed to initialize all components.",
                e.Message);
        }
    }

    private T? FindComponent<T>()
    {
        foreach (var comp in _uiComponents)
        {
            if (comp is T found)
            {
                return found;
            }
        }

        return default;
    }
}