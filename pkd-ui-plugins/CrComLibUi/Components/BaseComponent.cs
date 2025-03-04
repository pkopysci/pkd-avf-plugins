namespace CrComLibUi.Components;

using Crestron.SimplSharpPro.DeviceSupport;
using pkd_application_service.UserInterface;
using pkd_common_utils.Logging;
using Api;
using System;
using System.Collections.Generic;

internal abstract class BaseComponent(
	BasicTriListWithSmartObject ui,
	UserInterfaceDataContainer uiData)
	: IVueUiComponent, ISerialResponseHandler
{
	protected readonly Dictionary<string, Action<ResponseBase>> GetHandlers = new();
	protected readonly Dictionary<string, Action<ResponseBase>> PostHandlers = new();
	protected readonly UserInterfaceDataContainer UiData = uiData;
	protected readonly BasicTriListWithSmartObject Ui = ui;

	public bool Initialized { get; protected set; }

	/// <inheritdoc/>
	public abstract void HandleSerialResponse(string response);

	/// <inheritdoc/>
	public abstract void Initialize();

	/// <inheritdoc/>
	public virtual void SetActiveDefaults() { }

	/// <inheritdoc/>
	public virtual void SetStandbyDefaults() { }

	/// <inheritdoc/>
	public virtual void SendConfig() { }

	protected void Send(ResponseBase data, ApiHooks hook)
	{
		// serialize and send if successful
		var rxMessage = MessageFactory.SerializeMessage(data);
		if (string.IsNullOrEmpty(rxMessage)) return;
		Ui.StringInput[(uint)hook].StringValue = rxMessage;
		Ui.StringInput[(uint)hook].StringValue = string.Empty;
	}

	protected void Send(string data, ApiHooks hook)
	{
		Ui.StringInput[(uint)hook].StringValue = data;
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
		Ui.StringInput[(uint)hook].StringValue = string.Empty;
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