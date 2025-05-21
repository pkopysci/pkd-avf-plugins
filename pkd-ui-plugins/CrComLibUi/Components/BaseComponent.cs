namespace CrComLibUi.Components;

using System;
using System.Collections.Generic;
using Api;
using Crestron.SimplSharpPro.DeviceSupport;
using pkd_application_service.UserInterface;
using pkd_common_utils.Logging;

internal abstract class BaseComponent(
	BasicTriListWithSmartObject ui,
	UserInterfaceDataContainer uiData)
	: IVueUiComponent, ISerialResponseHandler
{
	protected readonly Dictionary<string, Action<ResponseBase>> GetHandlers = new();
	protected readonly Dictionary<string, Action<ResponseBase>> PostHandlers = new();
	protected readonly UserInterfaceDataContainer UiData = uiData;

	protected bool Initialized { get; set; }

	/// <inheritdoc/>
	public abstract void HandleSerialResponse(string response);

	/// <inheritdoc/>
	public abstract void Initialize();

	/// <inheritdoc/>
	public abstract void SendConfig();

	protected void Send(ResponseBase data, ApiHooks hook)
	{
		// serialize and send if successful
		var rxMessage = MessageFactory.SerializeMessage(data);
		if (string.IsNullOrEmpty(rxMessage)) return;
		ui.StringInput[(uint)hook].StringValue = rxMessage;
		ui.StringInput[(uint)hook].StringValue = string.Empty;
	}

	protected void Send(string data, ApiHooks hook)
	{
		ui.StringInput[(uint)hook].StringValue = data;
	}

	protected void SendServerError(ApiHooks hook)
	{
		var errRx = MessageFactory.CreateErrorResponse("500 - Internal Server Error.");
		Send(errRx, hook);
	}
	
	protected void SendError(string message, ApiHooks hook)
	{
		var errRx = MessageFactory.CreateErrorResponse(message);
		Send(errRx, hook);
	}
	
	protected void FlushJoinData(ApiHooks hook)
	{
		ui.StringInput[(uint)hook].StringValue = string.Empty;
	}

	protected bool CheckInitialized(string className, string methodName)
	{
		if (!Initialized)
		{
			Logger.Error($"GcuVueUi.{className}.{methodName}() - system not yet initialized.");
			return false;
		}

		return true;
	}
}