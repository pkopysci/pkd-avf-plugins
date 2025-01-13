namespace CrComLibUi.Components.ErrorReporting
{
	using Crestron.SimplSharpPro.DeviceSupport;
	using pkd_application_service.UserInterface;
	using pkd_common_utils.Logging;
	using pkd_ui_service.Interfaces;
	using CrComLibUi.Api;
	using System;
	using System.Collections.Generic;
	using System.Linq;

	internal class ErrorComponent : BaseComponent, IErrorInterface
	{
		private static readonly string COMMAND = "ERRLIST";
		private Dictionary<string, ErrorData> errors;

		public ErrorComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
			: base(ui, uiData)
		{
			errors = new Dictionary<string, ErrorData>();
			GetHandlers.Add(COMMAND, HandleGetErrors);
		}

		/// <inheritdoc/>
		public void AddDeviceError(string id, string label)
		{
			if (errors.ContainsKey(id))
			{
				return;
			}

			ErrorData data = new ErrorData()
			{
				Id = id,
				Message = label
			};

			errors.Add(id, data);
			SendErrors();
		}

		/// <inheritdoc/>
		public void ClearDeviceError(string id)
		{
			if (errors.TryGetValue(id, out ErrorData data))
			{
				errors.Remove(id);
				SendErrors();
			}
		}

		/// <inheritdoc/>
		public override void HandleSerialResponse(string response)
		{
			try
			{
				ResponseBase message = MessageFactory.DeserializeMessage(response);
				if (message == null)
				{
					ResponseBase errRx = MessageFactory.CreateErrorResponse("Invalid message format.");
					Send(errRx, ApiHooks.Errors);
					return;
				}

				if (message.Method == "GET")
				{
					GetHandlers[message.Command].Invoke(message);
				}
                else
                {
					ResponseBase errMessage = MessageFactory.CreateErrorResponse($"Unsupported method: {message.Method}.");
					Send(errMessage, ApiHooks.Errors);
					return;
				}
			}
			catch (Exception ex)
			{
				Logger.Error("CrComLibUi.ErrorComponent.HandleSerialResponse() - {0}", ex);
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Failed to parse message: {ex.Message}");
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
			ResponseBase response = MessageFactory.CreateGetResponseObject();
			response.Command = COMMAND;
			response.Data = errors.Values.ToArray();
			Send(response, ApiHooks.Errors);
		}
	}
}
