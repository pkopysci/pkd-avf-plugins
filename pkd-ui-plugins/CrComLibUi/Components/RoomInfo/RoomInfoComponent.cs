namespace CrComLibUi.Components.RoomInfo
{
	using Crestron.SimplSharpPro.DeviceSupport;
	using pkd_application_service.UserInterface;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using CrComLibUi.Api;
	using System;
	using System.Collections.Generic;

	internal class RoomInfoComponent : BaseComponent
	{
		private static readonly string USE_STATE_COMMAND = "USESTATE";
		private static readonly string GET_CONFIG_COMMAND = "CONFIG";
		private readonly bool isSecure;
		private readonly bool isTech;
		private bool useState;

		public RoomInfoComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData, string systemType)
			: base(ui, uiData)
		{
			SystemType = systemType;
			GetHandlers.Add(USE_STATE_COMMAND, HandleRequestGetConfig);
			GetHandlers.Add(GET_CONFIG_COMMAND, HandleRequestGetConfig);
			PostHandlers.Add(USE_STATE_COMMAND, HandleRequestPostUseState);
			isSecure = uiData.Tags.FindIndex(x => x.Contains("secure")) > -1;
			isTech = uiData.Tags.FindIndex(x => x.Contains("tech")) > -1;
		}

		public event EventHandler<GenericSingleEventArgs<bool>> StateChangeRequested;

		public string SystemType { get; set; }

		/// <inheritdoc/>
		public override void Initialize()
		{
			Initialized = false;
			if (string.IsNullOrEmpty(SystemType))
			{
				Logger.Error("CrComLibUi.RoomInfoComponent.Initialize() - Set SystemType first.");
				return;
			}

			if (uiData == null)
			{
				Logger.Error("CrComLibUi.RoomInfoComponent.Initialize() - Set UiData first.");
				return;
			}

			Initialized = true;
		}

		/// <inheritdoc/>
		public override void SetActiveDefaults()
		{
			if (!this.CheckInitialized(
				"RoomInfoComponent",
				nameof(SetActiveDefaults))) return;

			this.SetSystemState(true);
		}

		/// <inheritdoc/>
		public override void SetStandbyDefaults()
		{
			if (!this.CheckInitialized(
				"RoomInfoComponent",
				nameof(SetStandbyDefaults))) return;

			this.SetSystemState(false);
		}

		/// <inheritdoc/>
		public override void HandleSerialResponse(string response)
		{
			try
			{
				ResponseBase message = MessageFactory.DeserializeMessage(response);
				if (message == null)
				{
					ResponseBase errMessage = MessageFactory.CreateErrorResponse("Invalid message format.");
					Send(errMessage, ApiHooks.RoomConfig);
					return;
				}

				if (message.Method.Equals("GET"))
				{
					HandleGetRequest(message);
				}
				else if (message.Method.Equals("POST"))
				{
					HandlePostRequest(message);
				}
				else
				{
					ResponseBase errMessage = MessageFactory.CreateErrorResponse($"Unsupported method: {message.Method}.");
					Send(errMessage, ApiHooks.RoomConfig);
					return;
				}
			}
			catch (Exception ex)
			{
				Logger.Error("CrComLibUi.RoomInfoComponent.HandleSerialResponse() - {0}", ex);
				ResponseBase errMessage = MessageFactory.CreateErrorResponse($"Invalid message format: {ex.Message}");
				Send(errMessage, ApiHooks.RoomConfig);
			}
		}

		public void SetSystemState(bool state)
		{
			if (!CheckInitialized("RoomInfoComponent", "SetSystemState")) return;
			ResponseBase command = MessageFactory.CreateGetResponseObject();
			useState = state;
			command.Command = USE_STATE_COMMAND;
			command.Data = useState;
			Send(command, ApiHooks.RoomConfig);
		}

		public override void SendConfig()
		{
			Logger.Debug("CrComLibUserInterface - RoomInfoComponent.SendConfig()");

			ResponseBase rxObj = MessageFactory.CreateGetResponseObject();
			rxObj.Command = "CONFIG";
			HandleRequestGetConfig(rxObj);
		}

		private void HandleGetRequest(ResponseBase rxObj)
		{
			if (GetHandlers.TryGetValue(rxObj.Command, out var handler))
			{
				handler.Invoke(rxObj);
			}
			else
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Unsupported GET command: {rxObj.Command}");
				Send(errRx, ApiHooks.RoomConfig);
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
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Unsupported POST command: {rxObj.Command}");
				Send(errRx, ApiHooks.RoomConfig);
			}
		}

		private void HandleRequestGetConfig(ResponseBase rxObj)
		{
			Logger.Debug("RoomInfoComponent.HandleRequestGetConfig()");

			List<MainMenuItem> menuItems = new List<MainMenuItem>();
			foreach (var item in uiData.MenuItems)
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

			RoomConfigData config = new RoomConfigData()
			{
				IsInUse = useState,
				RoomName = uiData.Label,
				HelpNumber = uiData.HelpContact,
				IsSecure = isSecure,
				IsTech = isTech,
				RoomType = SystemType,
				DefaultActivity = uiData.DefaultActivity,
				MainMenu = menuItems
			};

			ResponseBase configResponse = MessageFactory.CreateGetResponseObject();
			configResponse.Command = GET_CONFIG_COMMAND;
			configResponse.Data = config;
			Send(configResponse, ApiHooks.RoomConfig);
		}

		private void HandleRequestGetUseState(ResponseBase rxObj)
		{
			rxObj.Data = useState;
			Send(rxObj, ApiHooks.RoomConfig);
		}

		private void HandleRequestPostUseState(ResponseBase rxObj)
		{
			try
			{
				var temp = StateChangeRequested;
				temp?.Invoke(this, new GenericSingleEventArgs<bool>(rxObj.Data));
			}
			catch (Exception ex)
			{
				ResponseBase errorRx = MessageFactory.CreateErrorResponse($"Invalid Data format: {ex.Message}");
				Send(errorRx, ApiHooks.RoomConfig);
			}
		}
	}
}
