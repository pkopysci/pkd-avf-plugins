using CrComLibUi.Api;

namespace CrComLibUi;

using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.UI;
using pkd_application_service.AudioControl;
using pkd_application_service.AvRouting;
using pkd_application_service.Base;
using pkd_application_service.DisplayControl;
using pkd_application_service.LightingControl;
using pkd_application_service.UserInterface;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;
using pkd_ui_service.Utility;
using Components.AudioControl;
using Components.CustomEvents;
using Components.ErrorReporting;
using Components.Lighting;
using Components.RoomInfo;
using Components.Security;
using Components.TransportControl;
using Components.VideoControl;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// Class for connecting to a Crestron TSW interface running a display project using a Json-based protocol via Crestron CrComLib protocol.
/// Once created, the following methods must be called to build the underlying configuration and listen for a connection:
/// SetUiData(), SetCrestronControl(), SetSystemType(), Initialize(), Connect()
/// </summary>
public class CrComLibUserInterface :
	IUserInterface,
	ICrestronUserInterface,
	IHtmlUserInterface,
	IRoutingUserInterface,
	IDisplayUserInterface,
	IAudioUserInterface,
	IAudioDiscreteLevelUserInterface,
	IErrorInterface,
	ILightingUserInterface,
	ITransportControlUserInterface,
	ICustomEventUserInterface,
	ISecurityUserInterface,
	IDisposable
{
	private readonly Dictionary<uint, Action<string>> _apiHandlerActions;
	private readonly List<IVueUiComponent> _uiComponents;
	private BasicTriListWithSmartObject? _ui;
	private CrestronControlSystem? _parent;
	private UserInterfaceDataContainer? _uiData;
	private uint _ipId;
	private bool _disposed;
	private string _systemType = string.Empty;

	/// <summary>
	/// Creates a new instance of <see cref="CrComLibUserInterface"/>.
	/// </summary>
	public CrComLibUserInterface()
	{
		IsXpanel = false;
		_uiComponents = [];
		_apiHandlerActions = new Dictionary<uint, Action<string>>();
	}

	~CrComLibUserInterface()
	{
		Dispose(false);
	}

	/// <inheritdoc/>
	public bool IsInitialized { get; private set; }

	/// <inheritdoc/>
	public bool IsOnline { get; private set; }

	/// <inheritdoc/>
	public bool IsXpanel { get; }

	/// <inheritdoc/>
	public string Id { get; private set; } = string.Empty;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<bool>>? SystemStateChangeRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? OnlineStatusChanged;

	/// <inheritdoc/>
	public event EventHandler? GlobalFreezeToggleRequest;

	/// <inheritdoc/>
	public event EventHandler? GlobalBlankToggleRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, string>>? AvRouteChangeRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, bool>>? DisplayPowerChangeRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? DisplayFreezeChangeRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? DisplayBlankChangeRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? DisplayScreenUpRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? DisplayScreenDownRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? StationLocalInputRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? StationLecternInputRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? AudioOutputLevelUpRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? AudioOutputLevelDownRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? AudioOutputMuteChangeRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? AudioInputLevelUpRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? AudioInputLevelDownRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? AudioInputMuteChangeRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputRouteRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, string>>? AudioZoneEnableToggleRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, int>>? SetAudioInputLevelRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, int>>? SetAudioOutputLevelRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, string>>? LightingSceneRecallRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericTrippleEventArgs<string, string, int>>? LightingLoadChangeRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, TransportTypes>>? TransportControlRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, string>>? TransportDialRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, string>>? TransportDialFavoriteRequest;

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, bool>>? CustomEventChangeRequest;

	/// <inheritdoc/>
	public void Connect(){}

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
		if (_uiComponents.Count == 0)
		{
			CreateComponents();
		}
	}

	/// <inheritdoc/>
	public void SetCrestronControl(CrestronControlSystem parent, int ipId)
	{
		if (ipId <= 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.SetCrestronControl() - argument 'ipId' must be greater than zero.");
			return;
		}

		_parent = parent;
		_ipId = (uint)ipId;
		RebuildInterface();
		_uiComponents.Clear();
		CreateComponents();
	}

	/// <inheritdoc/>
	public void SetSystemType(string systemType)
	{
		if (string.IsNullOrEmpty(systemType))
		{
			Logger.Error("CrComLibUserInterface.SetSystemType() - argument 'systemType' cannot be null or empty.");
			return;
		}

		_systemType = systemType;
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
			Logger.Error($"CrComLibUi.CrComLibUserInterface.Initialize() - failed to register UI: {_ui?.RegistrationFailureReason}");
			return;
		}

		foreach (var component in _uiComponents)
		{
			component.Initialize();
		}

		IsInitialized = true;
	}

	/// <inheritdoc/>
	public void HideSystemStateChanging()
	{
		Logger.Debug("TODO: CrComLibUserInterface.HideSystemStateChanging()");
	}

	/// <inheritdoc/>
	public void SetGlobalBlankState(bool state) => FindComponent<VideoControlComponent>()?.SetGlobalBlankState(state);

	/// <inheritdoc/>
	public void SetGlobalFreezeState(bool state) => FindComponent<VideoControlComponent>()?.SetGlobalFreezeState(state);

	/// <inheritdoc/>
	public void SetStationLecternInput(string id) => FindComponent<IDisplayUserInterface>()?.SetStationLecternInput(id);

	/// <inheritdoc/>
	public void SetStationLocalInput(string id) => FindComponent<IDisplayUserInterface>()?.SetStationLocalInput(id);

	/// <inheritdoc/>
	public void SetSystemState(bool state)
	{
		if (!IsInitialized)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.SetSystemState() - Not yet initialized.");
			return;
		}

		FindComponent<RoomInfoComponent>()?.SetSystemState(state);
	}

	/// <inheritdoc/>
	public void ShowSystemStateChanging(bool state) { }

	/// <inheritdoc/>
	public void UpdateAvRoute(AvSourceInfoContainer inputInfo, string outputId)
	{
		if (!IsInitialized)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.SetRoutingData() - Not yet initialized.");
			return;
		}

		FindComponent<IRoutingUserInterface>()?.UpdateAvRoute(inputInfo, outputId);
	}

	public void AddDeviceError(string id, string label)
	{
		FindComponent<IErrorInterface>()?.AddDeviceError(id, label);
	}

	public void ClearDeviceError(string id)
	{
		FindComponent<IErrorInterface>()?.ClearDeviceError(id);
	}

	/// <inheritdoc/>
	public void SetRoutingData(
		ReadOnlyCollection<AvSourceInfoContainer> sources,
		ReadOnlyCollection<InfoContainer> destinations,
		ReadOnlyCollection<InfoContainer> avRouters)
	{
		Logger.Debug("CrComLibUserInterface.SetRoutingData()");

		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.SetRoutingData() - Component data has not been set.");
			return;
		}

		FindComponent<IRoutingUserInterface>()?.SetRoutingData(sources, destinations, avRouters);
	}

	/// <inheritdoc/>
	public void SetDisplayData(ReadOnlyCollection<DisplayInfoContainer> displayData)
	{
		Logger.Debug("CrComLibUserInterface.SetDisplayData()");

		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.SetDisplayData() - Component data has not been set.");
			return;
		}

		FindComponent<IDisplayUserInterface>()?.SetDisplayData(displayData);
	}

	/// <inheritdoc/>
	public void UpdateDisplayPower(string id, bool state)
	{
		Logger.Debug("CrComLibUserInterface.UpdateDisplayPower()");
		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateDisplayPower() - Component data has not been set.");
			return;
		}

		FindComponent<IDisplayUserInterface>()?.UpdateDisplayPower(id, state);
	}

	/// <inheritdoc/>
	public void UpdateDisplayBlank(string id, bool state)
	{
		Logger.Debug("CrComLibUserInterface.UpdateDisplayBlank()");
		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateDisplayBlank() - Component data has not been set.");
			return;
		}

		FindComponent<IDisplayUserInterface>()?.UpdateDisplayBlank(id, state);
	}

	/// <inheritdoc/>
	public void UpdateDisplayFreeze(string id, bool state)
	{
		Logger.Debug("CrComLibUserInterface.UpdateDisplayFreeze()");
		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateDisplayFreeze() - Component data has not been set.");
			return;
		}

		FindComponent<IDisplayUserInterface>()?.UpdateDisplayFreeze(id, state);
	}

	/// <inheritdoc/>
	public void SetAudioData(
		ReadOnlyCollection<AudioChannelInfoContainer> inputs,
		ReadOnlyCollection<AudioChannelInfoContainer> outputs)
	{
		Logger.Debug("CrComLibUserInterface.SetAudioData()");

		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.SetAudioData() - Component data has not been set.");
			return;
		}

		FindComponent<IAudioUserInterface>()?.SetAudioData(inputs, outputs);
	}

	/// <inheritdoc/>
	public void UpdateAudioInputLevel(string id, int newLevel)
	{
		Logger.Debug("CrComLibUserInterface.UpdateAudioInputLevel()");

		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateAudioInputLevel() - Component data has not been set.");
			return;
		}
		FindComponent<IAudioUserInterface>()?.UpdateAudioInputLevel(id, newLevel);
	}

	/// <inheritdoc/>
	public void UpdateAudioInputMute(string id, bool muteState)
	{
		Logger.Debug("CrComLibUserInterface.UpdateAudioInputMute()");

		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateAudioInputMute() - Component data has not been set.");
			return;
		}
		FindComponent<IAudioUserInterface>()?.UpdateAudioInputMute(id, muteState);
	}

	/// <inheritdoc/>
	public void UpdateAudioOutputLevel(string id, int newLevel)
	{
		Logger.Debug("CrComLibUserInterface.UpdateAudioOutputLevel()");

		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateAudioOutputLevel() - Component data has not been set.");
			return;
		}
		FindComponent<IAudioUserInterface>()?.UpdateAudioOutputLevel(id, newLevel);
	}

	/// <inheritdoc/>
	public void UpdateAudioOutputMute(string id, bool muteState)
	{
		Logger.Debug("CrComLibUserInterface.UpdateAudioOutputMute()");

		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateAudioOutputMute() - Component data has not been set.");
			return;
		}
		FindComponent<IAudioUserInterface>()?.UpdateAudioOutputMute(id, muteState);
	}

	/// <inheritdoc/>
	public void UpdateAudioOutputRoute(string srcId, string destId)
	{
		Logger.Debug("CrComLibUserInterface.UpdateAudioOutputRoute()");

		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateAudioOutputRoute() - Component data has not been set.");
			return;
		}
		FindComponent<IAudioUserInterface>()?.UpdateAudioOutputRoute(srcId, destId);
	}

	/// <inheritdoc/>
	public void UpdateAudioZoneState(string channelId, string zoneId, bool newState)
	{
		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateAudioZoneState() - Component data has not been set.");
			return;
		}
		FindComponent<IAudioUserInterface>()?.UpdateAudioZoneState(channelId, zoneId, newState);
	}

	/// <inheritdoc/>
	public void SetLightingData(ReadOnlyCollection<LightingControlInfoContainer> lightingData)
	{
		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.SetLightingData() - Component data has not been set.");
			return;
		}
		FindComponent<ILightingUserInterface>()?.SetLightingData(lightingData);
	}

	/// <inheritdoc/>
	public void UpdateActiveLightingScene(string controlId, string sceneId)
	{
		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateActiveLightingScene() - Component data has not been set.");
			return;
		}
		FindComponent<ILightingUserInterface>()?.UpdateActiveLightingScene(controlId, sceneId);

	}

	/// <inheritdoc/>
	public void UpdateLightingZoneLoad(string controlId, string zoneId, int level)
	{
		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateLightingZoneLoad() - Component data has not been set.");
			return;
		}
		FindComponent<ILightingUserInterface>()?.UpdateLightingZoneLoad(controlId, zoneId, level);
	}

	/// <inheritdoc/>
	public void SetCableBoxData(ReadOnlyCollection<TransportInfoContainer> data)
	{
		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.SetCableBoxData() - Component data has not been set.");
			return;
		}
		FindComponent<ITransportControlUserInterface>()?.SetCableBoxData(data);
	}

	/// <inheritdoc/>
	public void AddCustomEvent(string eventTag, string label, bool state) =>
		FindComponent<ICustomEventUserInterface>()?.AddCustomEvent(eventTag, label, state);

	/// <inheritdoc/>
	public void UpdateCustomEvent(string eventTage, bool state) =>
		FindComponent<ICustomEventUserInterface>()?.UpdateCustomEvent(eventTage, state);

	/// <inheritdoc/>
	public void RemoveCustomEvent(string eventTag)
	{
		if (_uiComponents.Count < 0)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.RemoveCustomEvent() - Component data has not been set.");
			return;
		}
		FindComponent<ICustomEventUserInterface>()?.RemoveCustomEvent(eventTag);
	}

	/// <inheritdoc/>
	public void EnableSecurityPasscodeLock() => FindComponent<ISecurityUserInterface>()?.EnableSecurityPasscodeLock();

	/// <inheritdoc/>
	public void DisableTechOnlyLock() => FindComponent<ISecurityUserInterface>()?.DisableTechOnlyLock();

	/// <inheritdoc/>
	public void EnableTechOnlyLock() => FindComponent<ISecurityUserInterface>()?.EnableTechOnlyLock();
	
	private void Dispose(bool disposing)
	{
		if (_disposed) return;
		if (disposing)
		{
			_apiHandlerActions.Clear();
			_uiComponents.Clear();
			DisposeUi();
		}

		_disposed = true;
	}

	private void DisposeUi()
	{
		if (_ui == null) return;
		_ui.OnlineStatusChange -= Ui_OnlineStatusChange;
		_ui.SigChange -= Ui_SigChange;
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
				Logger.Error("CrComLibUi.CrComLibUserInterface.RebuildInterface() - Unsupported model: {0}", _uiData.Model);
				return;
		}

		_ui.SigChange += Ui_SigChange;
		_ui.OnlineStatusChange += Ui_OnlineStatusChange;
	}

	private void CreateComponents()
	{
		if (_ui == null || _uiData == null)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.CreateComponents() - ui or uiData has not been set.");
			return;
		}
		
		try
		{
			var ric = new RoomInfoComponent(_ui, _uiData, _systemType);
			ric.StateChangeRequested += SystemStateChangeRequestHandler;
			_uiComponents.Add(ric);
			_apiHandlerActions.Add((uint)ApiHooks.RoomConfig, ric.HandleSerialResponse);

			var vcc = new VideoControlComponent(_ui, _uiData);
			vcc.AvRouteChangeRequest += VideoRouteChangeRequestHandler;
			vcc.GlobalBlankToggleRequest += VideoBlankRequestHandler;
			vcc.GlobalFreezeToggleRequest += VideoFreezeRequestHandler;
			_uiComponents.Add(vcc);
			_apiHandlerActions.Add((uint)ApiHooks.VideoControl, vcc.HandleSerialResponse);
				
			var dcc = new DisplayControlComponent(_ui, _uiData);
			dcc.DisplayBlankChangeRequest += DisplayBlankChangeHandler;
			dcc.DisplayFreezeChangeRequest += DisplayFreezeChangeHandler;
			dcc.DisplayPowerChangeRequest += DisplayPowerChangeHandler;
			dcc.DisplayScreenDownRequest += DisplayScreenDownHandler;
			dcc.DisplayScreenUpRequest += DisplayScreenUpHandler;
			dcc.StationLecternInputRequest += DisplayLecternInputHandler;
			dcc.StationLocalInputRequest += DisplayLocalInputHandler;
			_uiComponents.Add(dcc);
			_apiHandlerActions.Add((uint)ApiHooks.DisplayChange, dcc.HandleSerialResponse);

			var acc = new AudioControlComponent(_ui, _uiData);
			acc.AudioInputLevelDownRequest += AudioInputDownHandler;
			acc.AudioInputLevelUpRequest += AudioInputUpHandler;
			acc.AudioInputMuteChangeRequest += AudioInputMuteHandler;
			acc.AudioOutputLevelUpRequest += AudioOutputUpHandler;
			acc.AudioOutputLevelDownRequest += AudioOutputDownHandler;
			acc.AudioOutputMuteChangeRequest += AudioOutputMuteHandler;
			acc.AudioOutputRouteRequest += AudioOutputRouteHandler;
			acc.AudioZoneEnableToggleRequest += AudioZoneToggleHandler;
			acc.SetAudioInputLevelRequest += AudioSetInputLevelHandler;
			acc.SetAudioOutputLevelRequest += AudioSetOutputLevelHandler;
			_uiComponents.Add(acc);
			_apiHandlerActions.Add((uint)ApiHooks.AudioControl, acc.HandleSerialResponse);

			var ecc = new ErrorComponent(_ui, _uiData);
			_apiHandlerActions.Add((uint)ApiHooks.Errors, ecc.HandleSerialResponse);
			_uiComponents.Add(ecc);

			var lcc = new LightingComponent(_ui, _uiData);
			lcc.LightingLoadChangeRequest += LightingSetLoadHandler;
			lcc.LightingSceneRecallRequest += LightingSceneHandler;
			_uiComponents.Add(lcc);
			_apiHandlerActions.Add((uint)ApiHooks.LightingControl, lcc.HandleSerialResponse);

			var tcc = new TransportComponent(_ui, _uiData);
			tcc.TransportDialFavoriteRequest += TransportFavoriteHandler;
			tcc.TransportControlRequest += TransportControlHandler;
			tcc.TransportDialRequest += TransportDialHandler;
			_uiComponents.Add(tcc);
			_apiHandlerActions.Add((uint)ApiHooks.DeviceControl, tcc.HandleSerialResponse);

			var securityComponent = new SecurityComponent(_ui, _uiData);
			_uiComponents.Add(securityComponent);
			_apiHandlerActions.Add((uint)ApiHooks.Security, securityComponent.HandleSerialResponse);

			var cec = new CustomEventComponent(_ui, _uiData);
			cec.CustomEventChangeRequest += HandleCustomEventRequest;
			_uiComponents.Add(cec);
			_apiHandlerActions.Add((uint)ApiHooks.Event, cec.HandleSerialResponse);
		}
		catch (Exception e)
		{
			Logger.Error("CrComLibUi.CrComLibUserInterface.CreateComponents() - Failed to initialize all components.", e.Message);
		}
	}

	private void HandleCustomEventRequest(object? sender, GenericDualEventArgs<string, bool> args)
	{
		var temp = CustomEventChangeRequest;
		temp?.Invoke(this, args);
	}

	private void TransportDialHandler(object? sender, GenericDualEventArgs<string, string> e)
	{
		var temp = TransportDialRequest;
		temp?.Invoke(this, e);
	}

	private void TransportControlHandler(object? sender, GenericDualEventArgs<string, TransportTypes> e)
	{
		var temp = TransportControlRequest;
		temp?.Invoke(this, e);
	}

	private void TransportFavoriteHandler(object? sender, GenericDualEventArgs<string, string> e)
	{
		var temp = TransportDialFavoriteRequest;
		temp?.Invoke(this, e);
	}

	private void LightingSceneHandler(object? sender, GenericDualEventArgs<string, string> e)
	{
		var temp = LightingSceneRecallRequest;
		temp?.Invoke(this, e);
	}

	private void LightingSetLoadHandler(object? sender, GenericTrippleEventArgs<string, string, int> e)
	{
		var temp = LightingLoadChangeRequest;
		temp?.Invoke(this, e);
	}

	private void AudioSetOutputLevelHandler(object? sender, GenericDualEventArgs<string, int> e)
	{
		var temp = SetAudioOutputLevelRequest;
		temp?.Invoke(this, e);
	}

	private void AudioSetInputLevelHandler(object? sender, GenericDualEventArgs<string, int> e)
	{
		var temp = SetAudioInputLevelRequest;
		temp?.Invoke(this, e);
	}

	private void AudioZoneToggleHandler(object? sender, GenericDualEventArgs<string, string> e) => Notify(AudioZoneEnableToggleRequest, e);

	private void AudioOutputRouteHandler(object? sender, GenericDualEventArgs<string, string> e) => Notify(AudioOutputRouteRequest, e);

	private void AudioOutputMuteHandler(object? sender, GenericSingleEventArgs<string> e) => Notify(AudioOutputMuteChangeRequest, e);

	private void AudioInputMuteHandler(object? sender, GenericSingleEventArgs<string> e) => Notify(AudioInputMuteChangeRequest, e);

	private void AudioOutputDownHandler(object? sender, GenericSingleEventArgs<string> e) => Notify(AudioOutputLevelDownRequest, e);

	private void AudioOutputUpHandler(object? sender, GenericSingleEventArgs<string> e) => Notify(AudioOutputLevelUpRequest, e);

	private void AudioInputUpHandler(object? sender, GenericSingleEventArgs<string> e) => Notify(AudioInputLevelUpRequest, e);

	private void AudioInputDownHandler(object? sender, GenericSingleEventArgs<string> e) => Notify(AudioInputLevelDownRequest, e);

	private void DisplayLocalInputHandler(object? sender, GenericSingleEventArgs<string> e) => Notify(StationLocalInputRequest, e);

	private void DisplayLecternInputHandler(object? sender, GenericSingleEventArgs<string> e) => Notify(StationLecternInputRequest, e);

	private void DisplayScreenUpHandler(object? sender, GenericSingleEventArgs<string> e) => Notify(DisplayScreenUpRequest, e);

	private void DisplayScreenDownHandler(object? sender, GenericSingleEventArgs<string> e) => Notify(DisplayScreenDownRequest, e);

	private void DisplayPowerChangeHandler(object? sender, GenericDualEventArgs<string, bool> e)
	{
		var temp = DisplayPowerChangeRequest;
		temp?.Invoke(this, e);
	}

	private void DisplayFreezeChangeHandler(object? sender, GenericSingleEventArgs<string> e) => Notify(DisplayFreezeChangeRequest, e);

	private void DisplayBlankChangeHandler(object? sender, GenericSingleEventArgs<string> e) => Notify(DisplayBlankChangeRequest, e);

	private void VideoFreezeRequestHandler(object? sender, EventArgs e)
	{
		var temp = GlobalFreezeToggleRequest;
		temp?.Invoke(this, e);
	}

	private void VideoBlankRequestHandler(object? sender, EventArgs e)
	{
		var temp = GlobalBlankToggleRequest;
		temp?.Invoke(this, e);
	}

	private void VideoRouteChangeRequestHandler(object? sender, GenericDualEventArgs<string, string> e) => Notify(AvRouteChangeRequest, e);

	private void Ui_SigChange(BasicTriList currentDevice, SigEventArgs args)
	{
		if (args.Sig.Type != eSigType.String) return;
		if (_apiHandlerActions.TryGetValue(args.Sig.Number, out var handler))
		{
			handler.Invoke(args.Sig.StringValue);
		}
	}

	private void Ui_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
	{
		IsOnline = args.DeviceOnLine;
		var temp = OnlineStatusChanged;
		temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
	}

	private void SystemStateChangeRequestHandler(object? sender, GenericSingleEventArgs<bool> e)
	{
		var temp = SystemStateChangeRequest;
		temp?.Invoke(this, e);
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

	private void Notify(EventHandler<GenericSingleEventArgs<string>>? handler, GenericSingleEventArgs<string> e)
	{
		handler?.Invoke(this, e);
	}

	private void Notify(EventHandler<GenericDualEventArgs<string, string>>? handler, GenericDualEventArgs<string, string> e)
	{
		handler?.Invoke(this, e);
	}
}