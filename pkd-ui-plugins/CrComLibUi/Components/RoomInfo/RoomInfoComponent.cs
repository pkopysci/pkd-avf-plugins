
using Crestron.SimplSharp;

namespace CrComLibUi.Components.RoomInfo;

using Api;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json.Linq;
using pkd_application_service.UserInterface;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using System;
using System.Collections.Generic;

internal class RoomInfoComponent : BaseComponent
{
	private const string UseStateCommand = "USESTATE";
	private const string GetConfigCommand = "CONFIG";
	private readonly bool _isSecure;
	private readonly bool _isTech;
	private readonly string _systemType;
	private bool _useState;

	public RoomInfoComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData, string systemType)
		: base(ui, uiData)
	{
		_systemType = systemType;
		GetHandlers.Add(UseStateCommand, HandleRequestGetUseState);
		GetHandlers.Add(GetConfigCommand, HandleRequestGetConfig);
		PostHandlers.Add(UseStateCommand, HandleRequestPostUseState);
		_isSecure = uiData.Tags.FindIndex(x => x.Contains("secure")) > -1;
		_isTech = uiData.Tags.FindIndex(x => x.Contains("tech")) > -1;
	}

	public event EventHandler<GenericSingleEventArgs<bool>>? StateChangeRequested;


	/// <inheritdoc/>
	public override void Initialize()
	{
		if (string.IsNullOrEmpty(_systemType))
		{
			Logger.Debug("CrComLibUi.RoomInfoComponent.Initialize() - SystemType has not been set.");
		}

		Initialized = true;
	}

	/// <inheritdoc/>
	public override void SetActiveDefaults()
	{
		if (!CheckInitialized(
			    "RoomInfoComponent",
			    nameof(SetActiveDefaults))) return;

		SetSystemState(true);
	}

	/// <inheritdoc/>
	public override void SetStandbyDefaults()
	{
		if (!this.CheckInitialized(
			    "RoomInfoComponent",
			    nameof(SetStandbyDefaults))) return;

		SetSystemState(false);
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
					SendError($"Unsupported method: {message.Method}.",ApiHooks.RoomConfig);
					break;
			}
		}
		catch (Exception ex)
		{
			var errMessage = MessageFactory.CreateErrorResponse($"Invalid message format: {ex.Message}");
			Send(errMessage, ApiHooks.RoomConfig);
		}
	}

	public void SetSystemState(bool state)
	{
		if (!CheckInitialized("RoomInfoComponent", "SetSystemState")) return;
		_useState = state;
		JProperty newState = new("UseState", state);
		var command = MessageFactory.CreateGetResponseObject();
		command.Command = UseStateCommand;
		command.Data.Add(newState);
		Send(command, ApiHooks.RoomConfig);
	}

	public override void SendConfig()
	{
		var rxObj = MessageFactory.CreateGetResponseObject();
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
			IsInUse = _useState,
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
		rxObj.Data.Add(new JProperty("UseState", _useState));
		Send(rxObj, ApiHooks.RoomConfig);
	}

	private void HandleRequestPostUseState(ResponseBase rxObj)
	{
		try
		{
			var state = rxObj.Data.Value<bool>("State");
			var temp = StateChangeRequested;
			temp?.Invoke(this, new GenericSingleEventArgs<bool>(state));
		}
		catch (Exception ex)
		{
			Logger.Error(ex, "CrComLibUi.RoomInfoComponent.HandleRequestPostUseState()");
			SendServerError(ApiHooks.RoomConfig);
		}
	}
}