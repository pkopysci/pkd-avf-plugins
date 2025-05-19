namespace CrComLibUi.Components.ErrorReporting;

using System;
using System.Collections.Generic;
using Api;
using Crestron.SimplSharpPro.DeviceSupport;
using Newtonsoft.Json.Linq;
using pkd_application_service.UserInterface;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;

internal class ErrorComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
    : BaseComponent(ui, uiData), IErrorInterface
{
    private const string Command = "ERRLIST";
    private const string AddCommand = "ADDERROR";
    private const string RemoveCommand = "REMOVEERROR";
    private readonly Dictionary<string, ErrorData> _errors = [];

    /// <inheritdoc/>
    public void AddDeviceError(string id, string label)
    {
        if (_errors.ContainsKey(id))
        {
            return;
        }

        var data = new ErrorData
        {
            Id = id,
            Message = label
        };

        _errors.Add(id, data);
        var command = MessageFactory.CreateGetResponseObject();
        command.Command = AddCommand;
        command.Data = JObject.FromObject(data);
        Send(command, ApiHooks.Errors);
    }

    /// <inheritdoc/>
    public void ClearDeviceError(string id)
    {
        if (!_errors.Remove(id)) return;
        var command = MessageFactory.CreateGetResponseObject();
        command.Command = RemoveCommand;
        command.Data = JObject.FromObject(new ErrorData { Id = id });
        Send(command, ApiHooks.Errors);
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
        message.Data["Errors"] = JToken.FromObject(_errors.Count == 0 ? Array.Empty<ErrorData>() : _errors.Values);
        Send(message, ApiHooks.Errors);
    }
}