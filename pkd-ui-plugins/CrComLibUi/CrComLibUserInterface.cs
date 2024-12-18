namespace CrComLibUi
{
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
	using CrComLibUi.Components.AudioControl;
	using CrComLibUi.Components.CustomEvents;
	using CrComLibUi.Components.ErrorReporting;
	using CrComLibUi.Components.Lighting;
	using CrComLibUi.Components.RoomInfo;
	using CrComLibUi.Components.Security;
	using CrComLibUi.Components.TransportControl;
	using CrComLibUi.Components.VideoControl;
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
		private readonly Dictionary<uint, Action<string>> apiHandlerActions;
		private readonly List<IVueUiComponent> uiComponents;
		private BasicTriListWithSmartObject ui;
		private CrestronControlSystem parent;
		private UserInterfaceDataContainer uiData;
		private uint ipId;
		private bool disposed;
		private string systemType;

		/// <summary>
		/// Creates a new instance of <see cref="CrComLibUserInterface"/>.
		/// </summary>
		public CrComLibUserInterface()
		{
			uiComponents = new List<IVueUiComponent>();
			apiHandlerActions = new Dictionary<uint, Action<string>>();
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
		public bool IsXpanel { get; private set; }

		/// <inheritdoc/>
		public string Id { get; private set; }

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<bool>> SystemStateChangeRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> OnlineStatusChanged;

		/// <inheritdoc/>
		public event EventHandler GlobalFreezeToggleRequest;

		/// <inheritdoc/>
		public event EventHandler GlobalBlankToggleRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AvRouteChangeRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, bool>> DisplayPowerChangeRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> DisplayFreezeChangeRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> DisplayBlankChangeRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> DisplayScreenUpRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> DisplayScreenDownRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> StationLocalInputRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> StationLecternInputRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> AudioOutputLevelUpRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> AudioOutputLevelDownRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> AudioOutputMuteChangeRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> AudioInputLevelUpRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> AudioInputLevelDownRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> AudioInputMuteChangeRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioOutputRouteRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioZoneEnableToggleRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, int>> SetAudioInputLevelRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, int>> SetAudioOutputLevelRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> LightingSceneRecallRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericTrippleEventArgs<string, string, int>> LightingLoadChangeRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, TransportTypes>> TransportControlRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> TransportDialRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> TransportDialFavoriteRequest;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, bool>> CustomEventChangeRequest;

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
			if (uiData == null)
			{
				Logger.Error("GcuVueInterface.SetUiData() - argument 'uiData' cannot be null.");
				return;
			}

			this.uiData = uiData;
			Id = uiData.Id;

			// create components if SetCrestronControl() has been called but compoents have not yet been created.
			if (ui != null && uiComponents.Count == 0)
			{
				CreateComponents();
			}
		}

		/// <inheritdoc/>
		public void SetCrestronControl(CrestronControlSystem parent, int ipId)
		{
			if (parent == null)
			{
				Logger.Error("CrComLibUi.CrComLibUserInterface.SetCreatronControl() - argument 'parent' cannot be null.");
				return;
			}

			if (ipId <= 0)
			{
				Logger.Error("CrComLibUi.CrComLibUserInterface.SetCreatronControl() - argument 'ipId' must be greater than zero.");
				return;
			}

			if (uiData == null)
			{
				Logger.Error("CrComLibUi.CrComLibUserInterface.SetCreatronControl() - Call SetUiData() first.");
				return;
			}

			this.parent = parent;
			this.ipId = (uint)ipId;
			RebuildInterface();
			DisposeComponents();
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

			this.systemType = systemType;
		}

		/// <inheritdoc/>
		public void Initialize()
		{
			Logger.Debug($"CrComLibUi.CrComLibUserInterface.Initialize() - {Id}");

			IsInitialized = false;
			if (ui == null)
			{
				Logger.Error("CrComLibUi.CrComLibUserInterface.Initialize() - call SetCrestronControl first.");
				return;
			}

			if (ui.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
			{
				Logger.Error(
                    "CrComLibUi.CrComLibUserInterface.Initialize() - failed to register UI: {0}",
					ui.RegistrationFailureReason);
				return;
			}

			foreach (var component in uiComponents)
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
				Logger.Error("CrComLibUi.CrComLibUserInterface.SetSystemState() - Not yet intialized.");
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
				Logger.Error("CrComLibUi.CrComLibUserInterface.SetRoutingData() - Not yet intialized.");
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
			//if (!IsInitialized)
			//{
			//	Logger.Error("CrComLibUi.CrComLibUserInterface.ClearDeviceError() - Not yet intialized.");
			//	return;
			//}

			FindComponent<IErrorInterface>()?.ClearDeviceError(id);
		}

		/// <inheritdoc/>
		public void SetRoutingData(
			ReadOnlyCollection<AvSourceInfoContainer> sources,
			ReadOnlyCollection<InfoContainer> destinations,
			ReadOnlyCollection<InfoContainer> avRouters)
		{
			Logger.Debug("CrComLibUserInterface.SetRoutingData()");

			if (uiComponents.Count < 0)
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

			if (uiComponents.Count < 0)
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
			if (uiComponents.Count < 0)
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
			if (uiComponents.Count < 0)
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
			if (uiComponents.Count < 0)
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

			if (uiComponents.Count < 0)
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

			if (uiComponents.Count < 0)
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

			if (uiComponents.Count < 0)
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

			if (uiComponents.Count < 0)
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

			if (uiComponents.Count < 0)
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

			if (uiComponents.Count < 0)
			{
				Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateAudioOutputRoute() - Component data has not been set.");
				return;
			}
			FindComponent<IAudioUserInterface>()?.UpdateAudioOutputRoute(srcId, destId);
		}

		/// <inheritdoc/>
		public void UpdateAudioZoneState(string channelId, string zoneId, bool newState)
		{
			if (uiComponents.Count < 0)
			{
				Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateAudioZoneState() - Component data has not been set.");
				return;
			}
			FindComponent<IAudioUserInterface>()?.UpdateAudioZoneState(channelId, zoneId, newState);
		}

		/// <inheritdoc/>
		public void SetLightingData(ReadOnlyCollection<LightingControlInfoContainer> lightingData)
		{
			if (uiComponents.Count < 0)
			{
				Logger.Error("CrComLibUi.CrComLibUserInterface.SetLightingData() - Component data has not been set.");
				return;
			}
			FindComponent<ILightingUserInterface>()?.SetLightingData(lightingData);
		}

		/// <inheritdoc/>
		public void UpdateActiveLightingScene(string controlId, string sceneId)
		{
			if (uiComponents.Count < 0)
			{
				Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateActiveLightingScene() - Component data has not been set.");
				return;
			}
			FindComponent<ILightingUserInterface>()?.UpdateActiveLightingScene(controlId, sceneId);

		}

		/// <inheritdoc/>
		public void UpdateLightingZoneLoad(string controlId, string zoneId, int level)
		{
			if (uiComponents.Count < 0)
			{
				Logger.Error("CrComLibUi.CrComLibUserInterface.UpdateLightingZoneLoad() - Component data has not been set.");
				return;
			}
			FindComponent<ILightingUserInterface>()?.UpdateLightingZoneLoad(controlId, zoneId, level);
		}

		/// <inheritdoc/>
		public void SetCableBoxData(ReadOnlyCollection<TransportInfoContainer> data)
		{
			if (uiComponents.Count < 0)
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
			if (uiComponents.Count < 0)
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

		/// <inheritdoc/>
		protected virtual void Dispose(bool disposing)
		{
			if (disposed) return;
			if (disposing)
			{
				apiHandlerActions.Clear();
				foreach (var component in uiComponents)
				{
					if (component is IDisposable disposable)
					{
						disposable.Dispose();
					}
				}

				this.uiComponents.Clear();
				DisposeUi();
			}

			disposed = true;
		}

		private void DisposeUi()
		{
			if (ui == null) return;

			ui.OnlineStatusChange -= Ui_OnlineStatusChange;
			ui.SigChange -= Ui_SigChange;
			ui.UnRegister();
			ui.Dispose();
			ui = null;
        }

		private bool RebuildInterface()
		{
			if (uiData == null)
			{
				return false;
			}

            if (ui !=  null)
			{
				DisposeUi();
			}

			switch (uiData.Model.ToUpper())
			{
				case "TSW770":
				case "TSW-770":
				case "TSW770-HTML": // backwards compatability with old configs
					ui = new Tsw770(ipId, parent);
					break;
				case "TSW1070":
				case "TSW-1070":
					ui = new Tsw1070(ipId, parent);
					break;
				default:
					Logger.Error("CrComLibUi.CrComLibUserInterface.RebuildInterface() - Unsupported model: {0}", uiData.Model);
					return false;
			}

			ui.SigChange += Ui_SigChange;
			ui.OnlineStatusChange += Ui_OnlineStatusChange;
			return true;
		}

		private bool CreateComponents()
		{
			if (ui == null) return false;

			Logger.Debug("CrComLibUserInterface.CreateComponents()");

			try
			{
				RoomInfoComponent ric = new RoomInfoComponent(ui, uiData, systemType);
				ric.StateChangeRequested += SystemStateChangeRequestHandler;
				uiComponents.Add(ric);
				apiHandlerActions.Add((uint)ApiHooks.RoomConfig, ric.HandleSerialResponse);

				VideoControlComponent vcc = new VideoControlComponent(ui, uiData);
				vcc.AvRouteChangeRequest += VideoRouteChangeRequestHandler;
				vcc.GlobalBlankToggleRequest += VideoBlankRequesthandler;
				vcc.GlobalFreezeToggleRequest += VideoFreezeRequestHandler;
				uiComponents.Add(vcc);
				apiHandlerActions.Add((uint)ApiHooks.VideoControl, vcc.HandleSerialResponse);
				
				DisplayControlComponent dcc = new DisplayControlComponent(ui, uiData);
				dcc.DisplayBlankChangeRequest += DisplayblankChangeHandler;
				dcc.DisplayFreezeChangeRequest += DisplayFreezeChangeHandler;
				dcc.DisplayPowerChangeRequest += DisplayPowerChangeHandler;
				dcc.DisplayScreenDownRequest += DisplayScreenDownHandler;
				dcc.DisplayScreenUpRequest += DisplayScreenUpHandler;
				dcc.StationLecternInputRequest += DisplayLecternInputHandler;
				dcc.StationLocalInputRequest += DisplayLocalInputHandler;
				uiComponents.Add(dcc);
				apiHandlerActions.Add((uint)ApiHooks.DisplayChange, dcc.HandleSerialResponse);

				AudioControlComponent acc = new AudioControlComponent(ui, uiData);
				acc.AudioInputLevelDownRequest += AudioInputDownHandler;
				acc.AudioInputLevelUpRequest += AudioInputUpHandler;
				acc.AudioInputMuteChangeRequest += AudioInputMuteHandler;
				acc.AudioOutputLevelUpRequest += AudioOutputUpHandlker;
				acc.AudioOutputLevelDownRequest += AudioOutputDownHandler;
				acc.AudioOutputMuteChangeRequest += AudioOutputMuteHandler;
				acc.AudioOutputRouteRequest += AudioOutputRouteHandler;
				acc.AudioZoneEnableToggleRequest += AudioZoneToggleHandler;
				acc.SetAudioInputLevelRequest += AudioSetInputLevelHandler;
				acc.SetAudioOutputLevelRequest += AudioSetOutputLevelHandler;
				uiComponents.Add(acc);
				apiHandlerActions.Add((uint)ApiHooks.AudioControl, acc.HandleSerialResponse);

				ErrorComponent ecc = new ErrorComponent(ui, uiData);
				apiHandlerActions.Add((uint)ApiHooks.Errors, ecc.HandleSerialResponse);
				uiComponents.Add(ecc);

				LightingComponent lcc = new LightingComponent(ui, uiData);
				lcc.LightingLoadChangeRequest += LightingSetLoadHandler;
				lcc.LightingSceneRecallRequest += LightingSceneHandler;
				uiComponents.Add(lcc);
				apiHandlerActions.Add((uint)ApiHooks.LightingControl, lcc.HandleSerialResponse);

				TransportComponent tcc = new TransportComponent(ui, uiData);
				tcc.TransportDialFavoriteRequest += TransportFavoritehandler;
				tcc.TransportControlRequest += TransportControlHandler;
				tcc.TransportDialRequest += TransportDialHandler;
				uiComponents.Add(tcc);
				apiHandlerActions.Add((uint)ApiHooks.DeviceControl, tcc.HandleSerialResponse);

				SecurityComponent securityComponent = new SecurityComponent(ui, uiData);
				uiComponents.Add(securityComponent);
				apiHandlerActions.Add((uint)ApiHooks.Security, securityComponent.HandleSerialResponse);

				CustomEventComponent cec = new CustomEventComponent(ui, uiData);
				cec.CustomEventChangeRequest += HandleCustomEventRequest;
				uiComponents.Add(cec);
				apiHandlerActions.Add((uint)ApiHooks.Event, cec.HandleSerialResponse);

				return true;
			}
			catch (Exception e)
			{
				Logger.Error("CrComLibUi.CrComLibUserInterface.CreateComponents() - Failed to initialize all components.", e.Message);
				return false;
			}
		}

		private void HandleCustomEventRequest(object sender, GenericDualEventArgs<string, bool> args)
		{
			var temp = this.CustomEventChangeRequest;
			temp?.Invoke(this, args);
		}

		private void TransportDialHandler(object sender, GenericDualEventArgs<string, string> e)
		{
			var temp = TransportDialRequest;
			temp?.Invoke(this, e);
		}

		private void TransportControlHandler(object sender, GenericDualEventArgs<string, pkd_ui_service.Utility.TransportTypes> e)
		{
			var temp = TransportControlRequest;
			temp?.Invoke(this, e);
		}

		private void TransportFavoritehandler(object sender, GenericDualEventArgs<string, string> e)
		{
			var temp = TransportDialFavoriteRequest;
			temp?.Invoke(this, e);
		}

		private void LightingSceneHandler(object sender, GenericDualEventArgs<string, string> e)
		{
			var temp = this.LightingSceneRecallRequest;
			temp?.Invoke(this, e);
		}

		private void LightingSetLoadHandler(object sender, GenericTrippleEventArgs<string, string, int> e)
		{
			var temp = this.LightingLoadChangeRequest;
			temp?.Invoke(this, e);
		}

		private void AudioSetOutputLevelHandler(object sender, GenericDualEventArgs<string, int> e)
		{
			var temp = SetAudioOutputLevelRequest;
			temp?.Invoke(this, e);
		}

		private void AudioSetInputLevelHandler(object sender, GenericDualEventArgs<string, int> e)
		{
			var temp = SetAudioInputLevelRequest;
			temp?.Invoke(this, e);
		}

		private void AudioZoneToggleHandler(object sender, GenericDualEventArgs<string, string> e) => Notify(AudioZoneEnableToggleRequest, e);

		private void AudioOutputRouteHandler(object sender, GenericDualEventArgs<string, string> e) => Notify(AudioOutputRouteRequest, e);

		private void AudioOutputMuteHandler(object sender, GenericSingleEventArgs<string> e) => Notify(AudioOutputMuteChangeRequest, e);

		private void AudioInputMuteHandler(object sender, GenericSingleEventArgs<string> e) => Notify(AudioInputMuteChangeRequest, e);

		private void AudioOutputDownHandler(object sender, GenericSingleEventArgs<string> e) => Notify(AudioOutputLevelDownRequest, e);

		private void AudioOutputUpHandlker(object sender, GenericSingleEventArgs<string> e) => Notify(AudioOutputLevelUpRequest, e);

		private void AudioInputUpHandler(object sender, GenericSingleEventArgs<string> e) => Notify(AudioInputLevelUpRequest, e);

		private void AudioInputDownHandler(object sender, GenericSingleEventArgs<string> e) => Notify(AudioInputLevelDownRequest, e);

		private void DisplayLocalInputHandler(object sender, GenericSingleEventArgs<string> e) => Notify(StationLocalInputRequest, e);

		private void DisplayLecternInputHandler(object sender, GenericSingleEventArgs<string> e) => Notify(StationLecternInputRequest, e);

		private void DisplayScreenUpHandler(object sender, GenericSingleEventArgs<string> e) => Notify(DisplayScreenUpRequest, e);

		private void DisplayScreenDownHandler(object sender, GenericSingleEventArgs<string> e) => Notify(DisplayScreenDownRequest, e);

		private void DisplayPowerChangeHandler(object sender, GenericDualEventArgs<string, bool> e)
		{
			var temp = this.DisplayPowerChangeRequest;
			temp?.Invoke(this, e);
		}

		private void DisplayFreezeChangeHandler(object sender, GenericSingleEventArgs<string> e) => Notify(DisplayFreezeChangeRequest, e);

		private void DisplayblankChangeHandler(object sender, GenericSingleEventArgs<string> e) => Notify(DisplayBlankChangeRequest, e);

		private void VideoFreezeRequestHandler(object sender, EventArgs e)
		{
			var temp = GlobalFreezeToggleRequest;
			temp?.Invoke(this, e);
		}

		private void VideoBlankRequesthandler(object sender, EventArgs e)
		{
			var temp = GlobalBlankToggleRequest;
			temp?.Invoke(this, e);
		}

		private void VideoRouteChangeRequestHandler(object sender, GenericDualEventArgs<string, string> e) => Notify(AvRouteChangeRequest, e);

		private void DisposeComponents()
		{
			foreach (var component in uiComponents)
			{
				if (component is IDisposable disposable)
				{
					disposable.Dispose();
				}
			}

			uiComponents.Clear();
		}

		private void Ui_SigChange(BasicTriList currentDevice, SigEventArgs args)
		{
			if (args.Sig.Type != eSigType.String) return;
			if (apiHandlerActions.TryGetValue(args.Sig.Number, out var handler))
			{
				handler.Invoke(args.Sig.StringValue);
			}
		}

		private void Ui_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
		{
			IsOnline = args.DeviceOnLine;
			var temp = this.OnlineStatusChanged;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));

			if (IsOnline)
			{
				foreach( var component in uiComponents)
				{
					component.SendConfig();
				}
			}
		}

		private void SystemStateChangeRequestHandler(object sender, GenericSingleEventArgs<bool> e)
		{
			var temp = SystemStateChangeRequest;
			temp?.Invoke(this, e);
		}

		private T FindComponent<T>()
		{
			foreach (var comp in uiComponents)
			{
				if (comp is T found)
				{
					return found;
				}
			}

			return default;
		}

		private void Notify(EventHandler<GenericSingleEventArgs<string>> handler, GenericSingleEventArgs<string> e)
		{
			var temp = handler;
			temp?.Invoke(this, e);
		}

		private void Notify(EventHandler<GenericDualEventArgs<string, string>> handler, GenericDualEventArgs<string, string> e)
		{
			var temp = handler;
			temp?.Invoke(this, e);
		}
	}
}
