namespace CrComLibUi.Components.CustomEvents;

using Crestron.SimplSharpPro.DeviceSupport;
using pkd_application_service.UserInterface;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;
using Api;
using System;
using System.Collections.Generic;
using System.Linq;

internal class CustomEventComponent : BaseComponent, ICustomEventUserInterface
{
	private const string CommandConfig = "CONFIG";
	private const string CommandState = "STATE";
	private readonly Dictionary<string,CustomEventData> _customEvents;

	public CustomEventComponent(
		BasicTriListWithSmartObject ui,
		UserInterfaceDataContainer uiData)
		: base(ui, uiData)
	{
		GetHandlers.Add(CommandConfig, HandleGetConfigRequest);
		PostHandlers.Add(CommandState, HandlePostStateRequest);
		_customEvents = new Dictionary<string, CustomEventData>();
	}

	public event EventHandler<GenericDualEventArgs<string, bool>>? CustomEventChangeRequest;

	/// <inheritdoc />
	public void UpdateCustomEvent(string eventTag, bool state)
	{
		if (_customEvents.TryGetValue(eventTag, out var eventData))
		{
			eventData.State = state;
			NotifyEventState(eventData);
		}
		else
		{
			Logger.Error("CrComLibUi.CustomEventComponent.UpdateCustomEvent() - no event with ID {0}", eventTag);
		}
	}

	/// <inheritdoc />
	public void AddCustomEvent(string eventTag, string label, bool state)
	{
		if (_customEvents.ContainsKey(eventTag))
		{
			Logger.Error("CrComLibUi.CustomEventComponent.AddCustomEvent() - event with ID {0} already exists.", eventTag);
		}
		else
		{
			_customEvents.Add(eventTag, new CustomEventData() {
				Id = eventTag,
				Label = label,
				State = state
			});
		}
	}

	/// <inheritdoc />
	public void RemoveCustomEvent(string eventTag)
	{
		_customEvents.Remove(eventTag);
	}

	/// <inheritdoc />
	public override void HandleSerialResponse(string response)
	{
		try
		{
			var message = MessageFactory.DeserializeMessage(response);
			if (message.Method.Equals(string.Empty))
			{
				var errMessage = MessageFactory.CreateErrorResponse("Invalid message format.");
				Send(errMessage, ApiHooks.Event);
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
					var errMessage = MessageFactory.CreateErrorResponse($"Unsupported method: {message.Method}.");
					Send(errMessage, ApiHooks.Event);
					return;
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.CustomEventComponent.HandleSerialResponse() - {0}", ex);
			var errMessage = MessageFactory.CreateErrorResponse("500 - Internal Server Error.");
			Send(errMessage, ApiHooks.Event);
		}
	}

	/// <inheritdoc />
	public override void Initialize()
	{
		Initialized = true;
	}

	private void HandleGetRequests(ResponseBase responseBase)
	{
		if (GetHandlers.TryGetValue(responseBase.Command, out var handler))
		{
			handler(responseBase);
		}
		else
		{
			var errRx = MessageFactory.CreateErrorResponse(
				$"Unsupported GET command: {responseBase.Command}");

			Send(errRx, ApiHooks.Event);
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
			var errRx = MessageFactory.CreateErrorResponse(
				$"Unsupported POST command: {responseBase.Command}");

			Send(errRx, ApiHooks.Event);
		}
	}

	private void HandleGetConfigRequest(ResponseBase responseBase)
	{
		Logger.Debug("CrComLibUi.CustomEventComponent.HandleGetConfigRequest()");
		responseBase.Data = _customEvents.Values.ToList();
		Send(responseBase, ApiHooks.Event);
	}

	private void HandlePostStateRequest(ResponseBase responseBase)
	{
		try
		{
			var temp = CustomEventChangeRequest;
			temp?.Invoke(this, new GenericDualEventArgs<string, bool>(
				responseBase.Data.Id,
				responseBase.Data.State));
		}
		catch (Exception ex)
		{
			var errRx = MessageFactory.CreateErrorResponse($"Invalid POST format: {ex.Message}");
			errRx.Command = responseBase.Command;
			Send(errRx, ApiHooks.Event);
		}
	}

	private void NotifyEventState(CustomEventData eventData)
	{
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandState;
		message.Data = eventData;
		Send(message, ApiHooks.Event);
	}
}