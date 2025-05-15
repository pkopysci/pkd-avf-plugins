namespace CrComLibUi.Components.VideoControl;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Api;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json.Linq;
using pkd_application_service.DisplayControl;
using pkd_application_service.UserInterface;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;

internal class DisplayControlComponent : BaseComponent, IDisplayUserInterface
{
	private const string CommandPower = "POWER";
	private const string CommandScreen = "SCREEN";
	private const string CommandInput = "INPUT";
	private const string CommandState = "STATUS";
	private const string CommandConfig = "CONFIG";
	private const string CommandFreeze = "FREEZE";
	private const string CommandBlank = "BLANK";

	private readonly List<Display> _displays;

	public DisplayControlComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
		: base(ui, uiData)
	{
		GetHandlers.Add(CommandConfig, HandleGetConfigRequest);
		GetHandlers.Add(CommandState, HandleGetStateRequest);
		PostHandlers.Add(CommandInput, HandlePostInputResponse);
		PostHandlers.Add(CommandScreen, HandlePostScreenResponse);
		PostHandlers.Add(CommandPower, HandlePostPowerResponse);
		PostHandlers.Add(CommandBlank, HandlePostBlankResponse);
		PostHandlers.Add(CommandFreeze, HandlePostFreezeResponse);
		_displays = [];
	}

	public event EventHandler<GenericDualEventArgs<string, bool>>? DisplayPowerChangeRequest;
	public event EventHandler<GenericSingleEventArgs<string>>? DisplayFreezeChangeRequest;
	public event EventHandler<GenericSingleEventArgs<string>>? DisplayBlankChangeRequest;
	public event EventHandler<GenericSingleEventArgs<string>>? DisplayScreenUpRequest;
	public event EventHandler<GenericSingleEventArgs<string>>? DisplayScreenDownRequest;
	public event EventHandler<GenericSingleEventArgs<string>>? StationLocalInputRequest;
	public event EventHandler<GenericSingleEventArgs<string>>? StationLecternInputRequest;

	public override void HandleSerialResponse(string response)
	{
		if (!CheckInitialized(
			    "DisplayControlComponent",
			    "HandleSerialResponse")) return;

		var rxObj = MessageFactory.DeserializeMessage(response);
		if (string.IsNullOrEmpty(rxObj.Method))
		{
			Send(MessageFactory.CreateErrorResponse("Invalid message format."), ApiHooks.DisplayChange);
			return;
		}

		var method = rxObj.Method.ToUpper();
		switch (method)
		{
			case "GET":
				HandleGetRequests(rxObj);
				break;
			case "POST":
				HandlePostRequests(rxObj);
				break;
			default:
				Send(MessageFactory.CreateErrorResponse(
						$"HTTP Method '{method}' not supported."),
					ApiHooks.DisplayChange);
				break;
		}
	}

	public override void Initialize()
	{
		Initialized = false;
		if (_displays.Count == 0)
		{
			Logger.Debug("CrComLibUi.DisplayControlComponent.Initialize() - No displays have been added for control.");
			return;
		}
		Initialized = true;
	}

	public override void SendConfig()
	{
		Logger.Debug("CrComLibUserInterface - DisplayControlComponent.SendConfig()");

		HandleGetConfigRequest(MessageFactory.CreateGetResponseObject());
	}

	public void SetDisplayData(ReadOnlyCollection<DisplayInfoContainer> displayData)
	{
		_displays.Clear();
		foreach (var item in displayData)
		{
			var display = new Display
			{
				Manufacturer = item.Manufacturer,
				Model = item.Model,
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
				display.Inputs.Add(new DisplayInput
				{
					Id = input.Id,
					Label = input.Label,
					Tags = input.Tags,
					Selected = false,
				});
				
				Logger.Debug($"DisplayControlComponent.SetDisplayData() - adding display {display.Manufacturer} {display.Model}");
			}

			_displays.Add(display);
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
		if (!CheckInitialized("DisplayControlComponent", "UpdateDisplayPower")) return;
		var display = FindDisplay("UpdateDisplayPower", id);
		if (display == null) return;
		display.PowerState = newState;
		SendDisplayStatus(display);
	}

	public void UpdateDisplayConnectionStatus(string id, bool isOnline)
	{
		if (!CheckInitialized("DisplayControlComponent", nameof(UpdateDisplayConnectionStatus))) return;
		var display = FindDisplay(nameof(UpdateDisplayConnectionStatus), id);
		if (display == null) return;
		display.IsOnline = isOnline;
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
				"CrComLibUi.DisplayControlComponent.HandleGetRequest() - Unknown command received: {0}",
				response.Command);

			SendError($"Unsupported command: {response.Command}", ApiHooks.DisplayChange);
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
			SendError($"Unsupported command: {response.Command}", ApiHooks.DisplayChange);
		}
	}

	private void HandleGetConfigRequest(ResponseBase response)
	{
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandConfig;
		message.Data["Displays"] = JToken.FromObject(_displays);
		Send(message, ApiHooks.DisplayChange);
	}

	private void HandleGetStateRequest(ResponseBase response)
	{
		Logger.Debug("CrComLibUi.DisplayControlComponent.HandleGetStateRequest()");

		try
		{
			var id = response.Data.Value<string>("Id");
			if (string.IsNullOrEmpty(id))
			{
				SendError("Invalid state GET request - missing Id.", ApiHooks.DisplayChange);
				return;
			}
			var display = FindDisplay("HandleGetStateRequest", id);
			if (display == null)
			{
				Send(MessageFactory.CreateErrorResponse(
						$"No display found with ID {id}"),
					ApiHooks.DisplayChange);
				return;
			}

			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CommandState;
			message.Data = new JObject(display);
			Send(message, ApiHooks.DisplayChange);
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.DisplayControlComponent.HandleGetStateRequest()", ex);
			SendServerError(ApiHooks.DisplayChange);
		}
	}

	private void HandlePostScreenResponse(ResponseBase response)
	{
		try
		{
			var id = response.Data.Value<string>("Id");
			var hasState = response.Data.ContainsKey("State");
			if (string.IsNullOrEmpty(id) || !hasState)
			{
				SendError("Invalid screen POST request - missing Id or State", ApiHooks.DisplayChange);
				return;
			}
			
			var temp = response.Data.Value<bool>("State") ?
				DisplayScreenUpRequest :
				DisplayScreenDownRequest;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(id));
		}
		catch (Exception ex)
		{
			Logger.Error("DisplayControlComponent.HandlePostScreenResponse:\n\r{0}", ex.Message);
			SendServerError(ApiHooks.DisplayChange);
		}
	}

	private void HandlePostPowerResponse(ResponseBase response)
	{
		var temp = DisplayPowerChangeRequest;
		if (temp == null) return;
		
		try
		{
			var id = response.Data.Value<string>("Id");
			var hasState = response.Data.ContainsKey("State");
			if (string.IsNullOrEmpty(id) || !hasState)
			{
				SendError("Invalid power POST request - missing Id or State", ApiHooks.DisplayChange);
				return;
			}
			
			temp.Invoke(this, new GenericDualEventArgs<string, bool>(id,
				response.Data.Value<bool>("State")));
		}
		catch (Exception ex)
		{
			Logger.Error("DisplayControlComponent.HandlePostPowerResponse:\n\r{0}", ex.Message);
			SendServerError(ApiHooks.DisplayChange);
		}
	}

	private void HandlePostInputResponse(ResponseBase response)
	{
		try
		{
			var targetId = response.Data.Value<string>("Id");
			var inputId = response.Data.Value<string>("InputId");
			if (string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(inputId))
			{
				SendError("Invalid input POST request - missing Id or InputId.", ApiHooks.DisplayChange);
				return;
			}

			var display = _displays.FirstOrDefault(x => x.Id == targetId);
			if (display == null)
			{
				SendError($"No display with ID {targetId}", ApiHooks.DisplayChange);
				return;
			}

			var input = display.Inputs.FirstOrDefault(x => x.Id == inputId);
			if (input == null)
			{
				SendError($"display {targetId} does not contain an input with ID {inputId}", ApiHooks.DisplayChange);
				return;
			}

			var temp = input.Tags.Contains("lectern") ? StationLecternInputRequest : StationLocalInputRequest;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(targetId));
		}
		catch (Exception ex)
		{
			Logger.Error("DisplayControlComponent.HandlePostInputResponse():\n\r{0}", ex.Message);
			SendServerError(ApiHooks.DisplayChange);
		}
	}

	private void HandlePostFreezeResponse(ResponseBase response)
	{
		try
		{
			var id = response.Data.Value<string>("Id");
			if (string.IsNullOrEmpty(id))
			{
				SendError("Invalid freeze POST request - missing Id.", ApiHooks.DisplayChange);
				return;
			}
			
			var temp = DisplayFreezeChangeRequest;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(id));
		}
		catch (Exception ex)
		{
			Logger.Error("DisplayControlComponent.HandlePostFreezeResponse():\n\r{0}", ex.Message);
			SendServerError(ApiHooks.DisplayChange);
		}
	}

	private void HandlePostBlankResponse(ResponseBase response)
	{
		Logger.Debug("CrComLibUi.DisplayControlComponent.HandlePostBlankResponse()");

		try
		{
			var id = response.Data.Value<string>("Id");
			if (string.IsNullOrEmpty(id))
			{
				SendError("Invalid blank POST request - missing Id.", ApiHooks.DisplayChange);
				return;
			}
			
			var temp = DisplayBlankChangeRequest;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(id));
		}
		catch (Exception ex)
		{
			Logger.Error("DisplayControlComponent.HandlePostBlankResponse():\n\r{0}", ex.Message);
			SendServerError(ApiHooks.DisplayChange);
		}
	}

	private Display? FindDisplay(string callingMethod, string id)
	{
		var display = _displays.FirstOrDefault(x => x.Id.Equals(id));
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
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandState;
		message.Data = JObject.FromObject(display);
		Send(message, ApiHooks.DisplayChange);
	}
}