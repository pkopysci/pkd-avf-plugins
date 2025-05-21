
namespace CrComLibUi.Components.CustomEvents;

using System;
using System.Collections.Generic;
using Api;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json.Linq;
using pkd_application_service.CustomEvents;
using pkd_application_service.UserInterface;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;

internal class CustomEventComponent(
	BasicTriListWithSmartObject ui,
	UserInterfaceDataContainer uiData,
	ICustomEventAppService appService)
	: BaseComponent(ui, uiData)
{
	private const string CommandConfig = "CONFIG";
	private const string CommandState = "STATE";
	private readonly Dictionary<string,CustomEventData> _customEvents = [];

	/// <inheritdoc />
	public override void HandleSerialResponse(string response)
	{
		try
		{
			var message = MessageFactory.DeserializeMessage(response);
			if (message.Method.Equals(string.Empty))
			{
				SendError("Invalid message format.", ApiHooks.Event);
				return;
			}

			switch (message.Method)
			{
				case "GET":
					HandleGetRequests(message);
					break;
				case "POST":
					HandlePostRequests(message);
					break;
				default:
				{
					SendError($"Unsupported method: {message.Method}.", ApiHooks.Event);
					return;
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.CustomEventComponent.HandleSerialResponse() - {0}", ex);
			SendServerError(ApiHooks.Event);
		}
	}

	/// <inheritdoc />
	public override void Initialize()
	{
		GetHandlers.Add(CommandConfig, HandleGetConfigRequest);
		PostHandlers.Add(CommandState, HandlePostStateRequest);

		var events = appService.QueryAllCustomEvents();
		foreach (var item in events)
		{
			if (_customEvents.ContainsKey(item.Id))
			{
				Logger.Error("CrComLibUi.CustomEventComponent.AddCustomEvent() - event with ID {0} already exists.", item.Id);
			}
			else
			{
				_customEvents.Add(item.Id, new CustomEventData() {
					Id = item.Id,
					Label = item.Label,
					State = appService.QueryCustomEventState(item.Id)
				});
			}
		}
		
		appService.CustomEventStateChanged += AppServiceOnCustomEventStateChanged;
		Initialized = true;
	}
	
	public override void SendConfig()
	{
		var response = MessageFactory.CreateGetResponseObject();
		response.Command = CommandConfig;
		HandleGetConfigRequest(response);
	}

	private void AppServiceOnCustomEventStateChanged(object? sender, GenericSingleEventArgs<string> e)
	{
		if (_customEvents.TryGetValue(e.Arg, out var eventData))
		{
			eventData.State = appService.QueryCustomEventState(e.Arg);
			NotifyEventState(eventData);
		}
		else
		{
			Logger.Error("CrComLibUi.CustomEventComponent.UpdateCustomEvent() - no event with ID {0}", e.Arg);
		}
	}
	
	private void HandleGetRequests(ResponseBase responseBase)
	{
		if (GetHandlers.TryGetValue(responseBase.Command, out var handler))
		{
			handler(responseBase);
		}
		else
		{
			SendError($"Unsupported GET command: {responseBase.Command}", ApiHooks.Event);
		}
	}

	private void HandlePostRequests(ResponseBase responseBase)
	{
		if (PostHandlers.TryGetValue(responseBase.Command, out var handler))
		{
			handler(responseBase);
		}
		else
		{
			SendError($"Unsupported POST command: {responseBase.Command}", ApiHooks.Event);
		}
	}

	private void HandleGetConfigRequest(ResponseBase responseBase)
	{
		try
		{
			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CommandConfig;
			message.Data = JObject.FromObject(_customEvents);
			Send(message, ApiHooks.Event);
		}
		catch (Exception e)
		{
			Logger.Error("CrComLibUi.CustomEventComponent.HandleGetConfigRequest() - {0}", e.Message);
			SendServerError(ApiHooks.Event);
		}
	}

	private void HandlePostStateRequest(ResponseBase responseBase)
	{
		try
		{
			var id = responseBase.Data.Value<string>("Id");
			var state = responseBase.Data.Value<bool>("State");
			if (string.IsNullOrEmpty(id) || !responseBase.Data.ContainsKey("State"))
			{
				SendError("Invalid POST event state: - missing Id or State.", ApiHooks.Event);
				return;
			}
			
			appService.ChangeCustomEventState(id, state);
		}
		catch (Exception e)
		{
			Logger.Error("CrComLibUi.CustomEventComponent.HandleGetConfigRequest() - {0}", e.Message);
			SendServerError(ApiHooks.Event);
		}
	}

	private void NotifyEventState(CustomEventData eventData)
	{
		try
		{
			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CommandState;
			message.Data["Id"] = eventData.Id;
			message.Data["State"] = eventData.State;
			Send(message, ApiHooks.Event);
		}
		catch (Exception e)
		{
			Logger.Error("CrComLibUi.CustomEventComponent.HandleGetConfigRequest() - {0}", e.Message);
			SendServerError(ApiHooks.Event);
		}
	}
}