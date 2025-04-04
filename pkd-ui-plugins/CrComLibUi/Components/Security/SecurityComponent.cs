﻿namespace CrComLibUi.Components.Security;

using Crestron.SimplSharpPro.DeviceSupport;
using pkd_application_service.UserInterface;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;
using Api;
using System;
using System.Linq;

internal class SecurityComponent : BaseComponent, ISecurityUserInterface
{
	private const string CheckPassword = "CODE";
	private const string TechLock = "TECHLOCK";
	private const string SetSecure = "SETSECURE";
	private string _passcode = string.Empty;

	public SecurityComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
		: base(ui, uiData)
	{
		PostHandlers.Add(CheckPassword, HandleUnlockRequest);
	}

	/// <inheritdoc />
	public void EnableSecurityPasscodeLock()
	{
		var cmd = MessageFactory.CreateGetResponseObject();
		cmd.Command = SetSecure;
		cmd.Data["State"] = true;
		Send(cmd, ApiHooks.Security);
	}

	/// <inheritdoc />
	public void DisableTechOnlyLock()
	{
		var cmd = MessageFactory.CreateGetResponseObject();
		cmd.Command = TechLock;
		cmd.Data["Code"] = string.Empty;
		cmd.Data["Result"] = false;
		Send(cmd, ApiHooks.Security);
	}

	/// <inheritdoc />
	public void EnableTechOnlyLock()
	{
		var cmd = MessageFactory.CreateGetResponseObject();
		cmd.Command = TechLock;
		cmd.Data["Code"] = string.Empty;
		cmd.Data["Result"] = true;
		Send(cmd, ApiHooks.Security);
	}

	/// <inheritdoc />
	public override void HandleSerialResponse(string response)
	{
		try
		{
			var message = MessageFactory.DeserializeMessage(response);
			if (string.IsNullOrEmpty(message.Method))
			{
				var errorMessage = MessageFactory.CreateErrorResponse("Invalid message format.");
				Send(errorMessage, ApiHooks.Security);
				return;
			}

			switch (message.Method)
			{
				case "GET":
					// handle get request at some point?
					break;
				case "POST":
					HandlePostRequest(message);
					break;
				default:
				{
					var errMessage = MessageFactory.CreateErrorResponse($"Unsupported method: {message.Method}.");
					Send(errMessage, ApiHooks.RoomConfig);
					break;
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.SecurityComponent.HandleSerialResponse() - {0}", ex);
			var errMessage = MessageFactory.CreateErrorResponse("500 - Internal Server Error.");
			Send(errMessage, ApiHooks.Security);
		}
	}

	/// <inheritdoc />
	public override void Initialize()
	{
		ParsePasscode();
		Initialized = true;
	}

	private void HandleUnlockRequest(ResponseBase response)
	{
		try
		{
			var userCode = response.Data.Value<string>("Code");
			if (string.IsNullOrEmpty(userCode))
			{
				SendError("Invalid POST unlock request - missing 'Code'.", ApiHooks.Security);
				return;
			}

			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CheckPassword;
			message.Data["Code"] = userCode;
			message.Data["Result"] = _passcode.Equals(userCode);
			
			Logger.Debug($"SecurityComponent.HandleUnlockRequest() - responding with {message.Data["Result"]}");
			
			Send(message, ApiHooks.Security);
		}
		catch (Exception e)
		{
			Logger.Error("CrComLibUi.SecurityComponent.HandleUnlockRequest() - {0}", e.Message);
			SendServerError(ApiHooks.Security);
		}
	}

	private void HandlePostRequest(ResponseBase response)
	{
		if (PostHandlers.TryGetValue(response.Command, out var handler))
		{
			handler(response);
		}
		else
		{
			var errRx = MessageFactory.CreateErrorResponse($"Unsupported POST command: {response.Command}");
			Send(errRx, ApiHooks.Security);
			Send(string.Empty, ApiHooks.Security);
		}
	}

	private void ParsePasscode()
	{
		if (UiData.Tags.Count == 0) return;

		try
		{
			var passcodeTag = UiData.Tags.FirstOrDefault(x => x.Contains("secure"));
			if (passcodeTag is { Length: > 0 })
			{
				_passcode = passcodeTag.Split('-')[1];
			}
		}
		catch (Exception ex)
		{
			Logger.Error(ex, "SecurityComponent.parsePasscode()");
		}
	}
}