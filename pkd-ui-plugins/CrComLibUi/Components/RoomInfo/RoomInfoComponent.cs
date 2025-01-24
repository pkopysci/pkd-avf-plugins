namespace CrComLibUi.Components.RoomInfo;

using Crestron.SimplSharpPro.DeviceSupport;
using pkd_application_service.UserInterface;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using Api;
using System;
using System.Collections.Generic;

internal class RoomInfoComponent : BaseComponent
{
	private const string UseStateCommand = "USESTATE";
	private const string GetConfigCommand = "CONFIG";
	private readonly bool _isSecure;
	private readonly bool _isTech;
	private bool _useState;

	public RoomInfoComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData, string systemType)
		: base(ui, uiData)
	{
		SystemType = systemType;
		GetHandlers.Add(UseStateCommand, HandleRequestGetUseState);
		GetHandlers.Add(GetConfigCommand, HandleRequestGetConfig);
		PostHandlers.Add(UseStateCommand, HandleRequestPostUseState);
		_isSecure = uiData.Tags.FindIndex(x => x.Contains("secure")) > -1;
		_isTech = uiData.Tags.FindIndex(x => x.Contains("tech")) > -1;
	}

	public event EventHandler<GenericSingleEventArgs<bool>>? StateChangeRequested;

	public string SystemType { get; }

	/// <inheritdoc/>
	public override void Initialize()
	{
		if (string.IsNullOrEmpty(SystemType))
		{
			Logger.Debug("CrComLibUi.RoomInfoComponent.Initialize() - SystemType has not been set.");
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
			var message = MessageFactory.DeserializeMessage(response);
			if (string.IsNullOrEmpty(message.Command))
			{
				var errMessage = MessageFactory.CreateErrorResponse("Invalid message format.");
				Send(errMessage, ApiHooks.RoomConfig);
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
				{
					var errMessage = MessageFactory.CreateErrorResponse($"Unsupported method: {message.Method}.");
					Send(errMessage, ApiHooks.RoomConfig);
					return;
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.RoomInfoComponent.HandleSerialResponse() - {0}", ex);
			var errMessage = MessageFactory.CreateErrorResponse($"Invalid message format: {ex.Message}");
			Send(errMessage, ApiHooks.RoomConfig);
		}
	}

	public void SetSystemState(bool state)
	{
		if (!CheckInitialized("RoomInfoComponent", "SetSystemState")) return;
		var command = MessageFactory.CreateGetResponseObject();
		_useState = state;
		command.Command = UseStateCommand;
		command.Data = _useState;
		Send(command, ApiHooks.RoomConfig);
	}

	public override void SendConfig()
	{
		Logger.Debug("CrComLibUserInterface - RoomInfoComponent.SendConfig()");

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
			var errRx = MessageFactory.CreateErrorResponse($"Unsupported GET command: {rxObj.Command}");
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
			var errRx = MessageFactory.CreateErrorResponse($"Unsupported POST command: {rxObj.Command}");
			Send(errRx, ApiHooks.RoomConfig);
		}
	}

	private void HandleRequestGetConfig(ResponseBase rxObj)
	{
		Logger.Debug("RoomInfoComponent.HandleRequestGetConfig()");

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
			IsInUse = _useState,
			RoomName = UiData.Label,
			HelpNumber = UiData.HelpContact,
			IsSecure = _isSecure,
			IsTech = _isTech,
			RoomType = SystemType,
			DefaultActivity = UiData.DefaultActivity,
			MainMenu = menuItems
		};

		var configResponse = MessageFactory.CreateGetResponseObject();
		configResponse.Command = GetConfigCommand;
		configResponse.Data = config;
		Send(configResponse, ApiHooks.RoomConfig);
	}

	private void HandleRequestGetUseState(ResponseBase rxObj)
	{
		rxObj.Data = _useState;
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
			var errorRx = MessageFactory.CreateErrorResponse($"Invalid Data format: {ex.Message}");
			Send(errorRx, ApiHooks.RoomConfig);
		}
	}
}