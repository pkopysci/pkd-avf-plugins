namespace CrComLibUi.Components.ErrorReporting;

using Crestron.SimplSharpPro.DeviceSupport;
using pkd_application_service.UserInterface;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;
using Api;
using System;
using System.Collections.Generic;
using System.Linq;

internal class ErrorComponent : BaseComponent, IErrorInterface
{
	private const string Command = "ERRLIST";
	private readonly Dictionary<string, ErrorData> _errors;

	public ErrorComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
		: base(ui, uiData)
	{
		_errors = new Dictionary<string, ErrorData>();
		GetHandlers.Add(Command, HandleGetErrors);
	}

	/// <inheritdoc/>
	public void AddDeviceError(string id, string label)
	{
		if (_errors.ContainsKey(id))
		{
			return;
		}

		var data = new ErrorData()
		{
			Id = id,
			Message = label
		};

		_errors.Add(id, data);
		SendErrors();
	}

	/// <inheritdoc/>
	public void ClearDeviceError(string id)
	{
		_errors.Remove(id);
		SendErrors();
	}

	/// <inheritdoc/>
	public override void HandleSerialResponse(string response)
	{
		try
		{
			var message = MessageFactory.DeserializeMessage(response);
			if (string.IsNullOrEmpty(message.Method))
			{
				var errRx = MessageFactory.CreateErrorResponse("Invalid message format.");
				Send(errRx, ApiHooks.Errors);
				return;
			}

			if (message.Method == "GET")
			{
				GetHandlers[message.Command].Invoke(message);
			}
			else
			{
				var errMessage = MessageFactory.CreateErrorResponse($"Unsupported method: {message.Method}.");
				Send(errMessage, ApiHooks.Errors);
			}
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.ErrorComponent.HandleSerialResponse() - {0}", ex);
			var errRx = MessageFactory.CreateErrorResponse("500 - Internal Server Error.");
			Send(errRx, ApiHooks.Errors);
		}
	}

	/// <inheritdoc/>
	public override void Initialize() { Initialized = true; }

	private void HandleGetErrors(ResponseBase response)
	{
		SendErrors();
	}

	private void SendErrors()
	{
		var response = MessageFactory.CreateGetResponseObject();
		response.Command = Command;
		response.Data = _errors.Values.ToArray();
		Send(response, ApiHooks.Errors);
	}
}