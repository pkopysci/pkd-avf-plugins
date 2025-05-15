namespace CrComLibUi.Components.ErrorReporting;

using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Api;
using Crestron.SimplSharpPro.DeviceSupport;
using pkd_application_service.UserInterface;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;

internal class ErrorComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
    : BaseComponent(ui, uiData), IErrorInterface
{
    private const string Command = "ERRLIST";
    private readonly Dictionary<string, ErrorData> _errors = [];

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
                SendError($"Unsupported method: {message.Method}.", ApiHooks.Errors);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("CrComLibUi.ErrorComponent.HandleSerialResponse() - {0}", ex);
            SendServerError(ApiHooks.Errors);
        }
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        GetHandlers.Clear();
        GetHandlers.Add(Command, HandleGetErrors);
        Initialized = true;
    }

    public override void SendConfig() => SendErrors();

    private void HandleGetErrors(ResponseBase response)
    {
        SendErrors();
    }

    private void SendErrors()
    {
        var message = MessageFactory.CreateGetResponseObject();
        message.Command = Command;
        message.Data = JObject.FromObject(_errors);
        Send(message, ApiHooks.Errors);
    }
}