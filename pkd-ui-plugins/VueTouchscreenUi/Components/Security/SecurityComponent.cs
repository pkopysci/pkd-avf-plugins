namespace CrComLibUi.Components.Security
{
	using Crestron.SimplSharpPro.DeviceSupport;
	using pkd_application_service.UserInterface;
	using pkd_common_utils.Logging;
	using pkd_ui_service.Interfaces;
	using CrComLibUi.Api;
	using System;
	using System.Linq;

	internal class SecurityComponent : BaseComponent, ISecurityUserInterface
	{
		private static readonly string CHECK_PASSWORD = "CODE";
		private static readonly string TECH_LOCK = "TECHLOCK";

		private string passcode = string.Empty;

		public SecurityComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
			: base(ui, uiData)
		{
			PostHandlers.Add(CHECK_PASSWORD, HandleUnlockRequest);
		}

		/// <inheritdoc />
		public void EnableSecurityPasscodeLock()
		{
			ResponseBase cmd = MessageFactory.CreateGetResponseObject();
			cmd.Command = CHECK_PASSWORD;
			cmd.Data = new SecurityData() { Code = string.Empty, Result = true };
			Send(cmd, ApiHooks.Security);
		}

		/// <inheritdoc />
		public void DisableTechOnlyLock()
		{
			ResponseBase cmd = MessageFactory.CreateGetResponseObject();
			cmd.Command = TECH_LOCK;
			cmd.Data = new SecurityData() { Code = string.Empty, Result = false };
			Send(cmd, ApiHooks.Security);
		}

		/// <inheritdoc />
		public void EnableTechOnlyLock()
		{
			ResponseBase cmd = MessageFactory.CreateGetResponseObject();
			cmd.Command = TECH_LOCK;
			cmd.Data = new SecurityData() { Code = string.Empty, Result = true };
			Send(cmd, ApiHooks.Security);
		}

		/// <inheritdoc />
		public override void HandleSerialResponse(string response)
		{
			try
			{
				ResponseBase message = MessageFactory.DeserializeMessage(response);
				if (message == null)
				{
					ResponseBase errorMessage = MessageFactory.CreateErrorResponse("Invalid message format.");
					Send(errorMessage, ApiHooks.Security);
					return;
				}

				if (message.Method.Equals("GET"))
				{
					// TODO: Handle GET requests?
				}
				else if (message.Method.Equals("POST"))
				{
					HandlPostRequest(message);
				}
				else
				{
					ResponseBase errMessage = MessageFactory.CreateErrorResponse($"Unsupported method: {message.Method}.");
					Send(errMessage, ApiHooks.RoomConfig);
				}
			}
			catch (Exception ex)
			{
				Logger.Error("GcuVueUi.SecurityComponent.HandleSerialResponse() - {0}", ex);
				ResponseBase errMessage = MessageFactory.CreateErrorResponse($"Invalid message format: {ex.Message}");
				Send(errMessage, ApiHooks.Security);
			}
		}

		/// <inheritdoc />
		public override void Initialize()
		{
			Initialized = false;
			ParsePasscode();
			Initialized = true;
		}

		private void HandleUnlockRequest(ResponseBase response)
		{
			try
			{
				bool result = passcode.Equals(response.Data.Code);
				ResponseBase returnData = MessageFactory.CreatePostResponseObject();
				returnData.Command = CHECK_PASSWORD;
				returnData.Data = new SecurityData() { Code = response.Data.Code, Result = result };
				Send(returnData, ApiHooks.Security);
				FlushJoinData(ApiHooks.Security);
			}
			catch (Exception ex)
			{
				Logger.Error(ex, "SecurityComponent.HandleUnlockRequest()");
				Send(MessageFactory.CreateErrorResponse($"Invalid passcode: {ex.Message}"), ApiHooks.Security);
			}
		}

		private void HandlPostRequest(ResponseBase response)
		{
			if (PostHandlers.TryGetValue(response.Command, out Action<ResponseBase> handler))
			{
				handler(response);
			}
			else
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Unsupported POST command: {response.Command}");
				Send(errRx, ApiHooks.Security);
			}
		}

		private void ParsePasscode()
		{
			if (uiData.Tags == null || uiData.Tags.Count == 0) return;

			try
			{
				string passcodeTag = uiData.Tags.FirstOrDefault(x => x.Contains("secure"));
				if (passcodeTag != null && passcodeTag.Length > 0)
				{
					passcode = passcodeTag.Split('-')[1];
				}
			}
			catch (Exception ex)
			{
				Logger.Error(ex, "SecurityComponent.parsePasscode()");
			}
		}
	}
}
