namespace CrComLibUi.Components.VideoControl
{
	using Crestron.SimplSharpPro.DeviceSupport;
	using pkd_application_service.DisplayControl;
	using pkd_application_service.UserInterface;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_ui_service.Interfaces;
	using CrComLibUi.Api;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;

	internal class DisplayControlComponent : BaseComponent, IDisplayUserInterface
	{
		private static readonly string SUBJECT = "DISPLAY";
		private static readonly string COMMAND_POWER = "POWER";
		private static readonly string COMMAND_SCREEN = "SCREEN";
		private static readonly string COMMAND_INPUT = "INPUT";
		private static readonly string COMMAND_STATE = "STATUS";
		private static readonly string COMMAND_CONFIG = "CONFIG";
		private static readonly string COMMAND_FREEZE = "FREEZE";
		private static readonly string COMMAND_BLANK = "BLANK";

		private List<Display> displays;

		public DisplayControlComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
			: base(ui, uiData)
		{
			GetHandlers.Add(COMMAND_CONFIG, HandleGetConfigRequest);
			GetHandlers.Add(COMMAND_STATE, HandleGetStateRequest);
			PostHandlers.Add(COMMAND_INPUT, HandlePostInputResponse);
			PostHandlers.Add(COMMAND_SCREEN, HandlePostScreenResponse);
			PostHandlers.Add(COMMAND_POWER, HandlePostPowerResponse);
			PostHandlers.Add(COMMAND_BLANK, HandlePostBlankResponse);
			PostHandlers.Add(COMMAND_FREEZE, HandlePostFreezeResponse);
		}

		public event EventHandler<GenericDualEventArgs<string, bool>> DisplayPowerChangeRequest;
		public event EventHandler<GenericSingleEventArgs<string>> DisplayFreezeChangeRequest;
		public event EventHandler<GenericSingleEventArgs<string>> DisplayBlankChangeRequest;
		public event EventHandler<GenericSingleEventArgs<string>> DisplayScreenUpRequest;
		public event EventHandler<GenericSingleEventArgs<string>> DisplayScreenDownRequest;
		public event EventHandler<GenericSingleEventArgs<string>> StationLocalInputRequest;
		public event EventHandler<GenericSingleEventArgs<string>> StationLecternInputRequest;

		public override void HandleSerialResponse(string response)
		{
			if (!CheckInitialized(
				"DisplayControlComponent",
				"HandleSerialResponse")) return;

			ResponseBase rxObj = MessageFactory.DeserializeMessage(response);
			if (rxObj == null)
			{
				Send(MessageFactory.CreateErrorResponse("Invalid message format."), ApiHooks.DisplayChange);

				return;
			}

			string method = rxObj.Method.ToUpper();
			if (method.Equals("GET"))
			{
				HandleGetRequests(rxObj);
			}
			else if (method.Equals("POST"))
			{
				HandlePostRequests(rxObj);
			}
			else
			{
				Send(MessageFactory.CreateErrorResponse(
					$"HTTP Method '{method}' not supported."),
					ApiHooks.DisplayChange);
			}
		}

		public override void Initialize()
		{
			Initialized = false;
			if (uiData == null)
			{
				Logger.Error("CrComLibUi.DisplayControlComponent.Initialize() - Set UiData first.");
				return;
			}

			if (displays == null)
			{
				Logger.Error("CrComLibUi.DisplayControlComponent.Initialize() - set display data first (call SetDisplayData()).");
				return;
			}

			Initialized = true;
		}

        public override void SendConfig()
        {
			Logger.Debug("CrComLibUserInterface -DisplayControlComponent.SendConfig()");

			HandleGetConfigRequest(MessageFactory.CreateGetResponseObject());
        }

        public void SetDisplayData(ReadOnlyCollection<DisplayInfoContainer> displayData)
		{
			if (displayData == null)
			{
				Logger.Error("CrComLibUi.DisplayControlComponent.SetDisplayData() - argument 'displayData' cannot be null.");
				return;
			}

			displays = new List<Display>();
            foreach (var item in displayData)
			{
				Display disp = new Display()
				{
					Id = item.Id,
					Label = item.Label,
					Icon = item.Icon,
					Tags = item.Tags,
					IsOnline = item.IsOnline,
					HasScreen = item.HasScreen,
					PowerState = false,
					Blank = false,
					Freeze = false,
				};

				foreach (var input in item.Inputs)
				{
					disp.Inputs.Add(new DisplayInput()
					{
						Id = input.Id,
						Label = input.Label,
						Tags = input.Tags,
						Selected = false,
					});
				}

				displays.Add(disp);
			}
		}

		public void SetStationLecternInput(string id)
		{
			if (!CheckInitialized("DisplayControlComponent", "SetStationLecternInput"))
				return;

			var display = FindDisplay("SetStationLecternInput", id);
			if (display == null) return;

			foreach (var input in display.Inputs)
			{
				input.Selected = input.Tags.Contains("lectern");
			}

			SendDisplayStatus(display);
		}

		public void SetStationLocalInput(string id)
		{
			if (!CheckInitialized("DisplayControlComponent", "SetStationLocalInput"))
				return;

			var display = FindDisplay("SetStationLocalInput", id);
			if (display == null) return;

			foreach (var input in display.Inputs)
			{
				input.Selected = input.Tags.Contains("station");
			}

			SendDisplayStatus(display);
		}

		public void UpdateDisplayBlank(string id, bool newState)
		{
			if (!CheckInitialized("DisplayControlComponent", "UpdateDisplayBlank"))
				return;

			var display = FindDisplay("UpdateDisplayBlank", id);
			if (display == null) return;

			display.Blank = newState;
			SendDisplayStatus(display);
		}

		public void UpdateDisplayFreeze(string id, bool newState)
		{
			if (!CheckInitialized("DisplayControlComponent", "UpdateDisplayFreeze"))
				return;

			var display = FindDisplay("UpdateDisplayFreeze", id);
			if (display == null) return;

			display.Freeze = newState;
			SendDisplayStatus(display);
		}

		public void UpdateDisplayPower(string id, bool newState)
		{
			if (!CheckInitialized("DisplayControlComponent", "UpdateDisplayPower"))
				return;

			var display = FindDisplay("UpdateDisplayPower", id);
			if (display == null) return;

			display.PowerState = newState;
			SendDisplayStatus(display);
		}

		private void HandleGetRequests(ResponseBase response)
		{
			if (GetHandlers.TryGetValue(response.Command.ToUpper(), out var handler))
			{
				handler.Invoke(response);
			}
			else
			{
				Logger.Error(
					"CrComLibUi.DisplayControlComponent.HandleGetRequest() - Unknown command recieved: {0}",
					response.Command);

				Send(MessageFactory.CreateErrorResponse(
					$"Unsupported command: {response.Command}"),
					ApiHooks.DisplayChange);
			}
		}

		private void HandlePostRequests(ResponseBase response)
		{
			if (PostHandlers.TryGetValue(response.Command.ToUpper(), out var handler))
			{
				handler.Invoke(response);
			}
			else
			{
				Send(MessageFactory.CreateErrorResponse(
					$"Unsupported command: {response.Command}"),
					ApiHooks.DisplayChange);
			}
		}

		private void HandleGetConfigRequest(ResponseBase response)
		{
			ResponseBase message = MessageFactory.CreateGetResponseObject();
			message.Command = COMMAND_CONFIG;
			message.Data = displays;
			Send(message, ApiHooks.DisplayChange);
		}

		private void HandleGetStateRequest(ResponseBase response)
		{
			Logger.Debug("CrComLibUi.DisplayControlComponent.HandleGetStateRequest()");

			try
			{
				var display = FindDisplay("HandleGetStateRequest", response.Data.Id);
				if (display == null)
				{
					Send(MessageFactory.CreateErrorResponse(
						$"No display found with ID {response.Data.Id}"),
						ApiHooks.DisplayChange);
					return;
				}

				ResponseBase message = MessageFactory.CreateGetResponseObject();
				message.Command = COMMAND_STATE;
				message.Data = display;
				Send(message, ApiHooks.DisplayChange);
			}
			catch (Exception ex)
			{
				Send(MessageFactory.CreateErrorResponse(
					$"Failed to parse state request: {ex.Message}"),
					ApiHooks.DisplayChange);
			}
		}

		private void HandlePostScreenResponse(ResponseBase response)
		{
			try
			{
				var temp = response.Data.State ? DisplayScreenUpRequest : DisplayScreenDownRequest;
				temp?.Invoke(this, new GenericSingleEventArgs<string>(response.Data.Id));
			}
			catch (Exception ex)
			{
				Send(MessageFactory.CreateErrorResponse(
					$"Failed to parse screen request: {ex.Message}"),
					ApiHooks.DisplayChange);
			}
		}

		private void HandlePostPowerResponse(ResponseBase response)
		{
			try
			{
				var temp = DisplayPowerChangeRequest;
				temp?.Invoke(this, new GenericDualEventArgs<string, bool>(response.Data.Id, response.Data.State));
			}
			catch (Exception ex)
			{
				Send(MessageFactory.CreateErrorResponse(
					$"Failed to parse power request: {ex.Message}"),
					ApiHooks.DisplayChange);
			}
		}

		private void HandlePostInputResponse(ResponseBase response)
		{
			try
			{
				var display = displays.FirstOrDefault(x => x.Id == response.Data.Id);
				if (display == null)
				{
					Send(MessageFactory.CreateErrorResponse(
						$"No display with ID {response.Data.Id}"),
						ApiHooks.DisplayChange);
					return;
				}

				var input = display.Inputs.FirstOrDefault(x => x.Id == response.Data.InputId);
				if (input == null)
				{
					Send(MessageFactory.CreateErrorResponse(
						$"display {display.Id} does not contain an input with ID {response.Data.InputId}"),
						ApiHooks.DisplayChange);
					return;
				}

				var temp = input.Tags.Contains("lectern") ? StationLecternInputRequest : StationLocalInputRequest;
				temp?.Invoke(this, new GenericSingleEventArgs<string>(display.Id));
			}
			catch (Exception ex)
			{
				Send(MessageFactory.CreateErrorResponse(
					$"Failed to parse display input request: {ex.Message}"),
					ApiHooks.DisplayChange);
			}
		}

		private void HandlePostFreezeResponse(ResponseBase response)
		{
			Logger.Debug("CrComLibUi.DisplayControlComponent.HandlePostFreezeResponse()");

			try
			{
				var temp = DisplayFreezeChangeRequest;
				temp?.Invoke(this, new GenericSingleEventArgs<string>(response.Data.Id));
			}
			catch (Exception ex)
			{
				Send(MessageFactory.CreateErrorResponse(
					$"Failed to parse display freeze request: {ex.Message}"),
					ApiHooks.DisplayChange);
			}
		}

		private void HandlePostBlankResponse(ResponseBase response)
		{
			Logger.Debug("CrComLibUi.DisplayControlComponent.HandlePostBlankResponse()");

			try
			{
				var temp = DisplayBlankChangeRequest;
				temp?.Invoke(this, new GenericSingleEventArgs<string>(response.Data.Id));
			}
			catch (Exception ex)
			{
				Send(MessageFactory.CreateErrorResponse(
					$"Failed to parse display blank request: {ex.Message}"),
					ApiHooks.DisplayChange);
			}
		}

		private Display FindDisplay(string callingMethod, string id)
		{
			var display = displays.FirstOrDefault(x => x.Id.Equals(id));
			if (display == null)
			{
				Logger.Error(
					"CrComLibUi.DisplayControlComponent.{0}() - no display found with ID {1}",
					callingMethod,
					id);
				return null;
			}

			return display;
		}

		private void SendDisplayStatus(Display display)
		{
			ResponseBase message = MessageFactory.CreateGetResponseObject();
			message.Command = COMMAND_STATE;
			message.Data = display;
			Send(message, ApiHooks.DisplayChange);
		}
	}
}
