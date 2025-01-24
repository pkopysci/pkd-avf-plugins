namespace CrComLibUi.Components.TransportControl;

using Crestron.SimplSharpPro.DeviceSupport;
using pkd_application_service.Base;
using pkd_application_service.UserInterface;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;
using pkd_ui_service.Utility;
using Api;
using System;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;

internal class TransportComponent : BaseComponent, ITransportControlUserInterface
{
	private const string CommandConfig = "CONFIG";
	private const string CommandTransport = "TRANSPORT";
	private const string CommandFavorite = "FAVORITE";
	private const string CommandChannel = "CHANNEL";
	private ReadOnlyCollection<TransportInfoContainer> _devices;

	public TransportComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
		: base(ui, uiData)
	{
		GetHandlers.Add(CommandConfig, HandleGetConfigRequest);
		PostHandlers.Add(CommandFavorite, HandlePostFavoriteRequest);
		PostHandlers.Add(CommandTransport, HandlePostTransportRequest);
		PostHandlers.Add(CommandChannel, HandlePostChannelRequest);
		_devices = new ReadOnlyCollection<TransportInfoContainer>([]);
	}

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, TransportTypes>>? TransportControlRequest;
		
	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, string>>? TransportDialRequest;
		
	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, string>>? TransportDialFavoriteRequest;

	/// <inheritdoc/>
	public override void HandleSerialResponse(string response)
	{
		try
		{
			var message = MessageFactory.DeserializeMessage(response);
			if (string.IsNullOrEmpty(message.Method))
			{
				var errMessage = MessageFactory.CreateErrorResponse("Invalid message format.");
				Send(errMessage, ApiHooks.DeviceControl);
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
					Send(errMessage, ApiHooks.DeviceControl);
					return;
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.TransportComponent.HandleSerialResponse() - {0}", ex);
			var errMessage = MessageFactory.CreateErrorResponse($"Invalid message format: {ex.Message}");
			Send(errMessage, ApiHooks.DeviceControl);
		}
	}

	/// <inheritdoc/>
	public override void Initialize()
	{
		if(_devices.Count == 0)
			Logger.Debug("CrComLibUi.TransportComponent.Initialize() - no device data set.");
		Initialized = true;
	}

	/// <inheritdoc/>
	public void SetCableBoxData(ReadOnlyCollection<TransportInfoContainer> data)
	{
		_devices = data;
		if (!Initialized) return;
		var rx = MessageFactory.CreateGetResponseObject();
		rx.Command = CommandConfig;
		rx.Data = _devices;
		Send(rx, ApiHooks.DeviceControl);
	}

	private void HandleGetRequest(ResponseBase message)
	{
		if (GetHandlers.TryGetValue(message.Command, out var handler))
		{
			handler.Invoke(message);
		}
		else
		{
			var errRx = MessageFactory.CreateErrorResponse($"Unsupported GET command: {message.Command}");
			Send(errRx, ApiHooks.DeviceControl);
		}
	}

	private void HandlePostRequest(ResponseBase message)
	{
		if (PostHandlers.TryGetValue(message.Command, out var handler))
		{
			handler.Invoke(message);
		}
		else
		{
			var errRx = MessageFactory.CreateErrorResponse($"Unsupported POST command: {message.Command}");
			Send(errRx, ApiHooks.DeviceControl);
		}
	}

	private void HandleGetConfigRequest(ResponseBase message)
	{
		var rx = MessageFactory.CreateGetResponseObject();
		rx.Command = CommandConfig;
		rx.Data = _devices;
		Send(rx, ApiHooks.DeviceControl);
	}

	private void HandlePostFavoriteRequest(ResponseBase message)
	{
		Logger.Debug("TransportComponent.HandlePostFavoriteRequest()");

		try
		{
			//JObject data = message.Data as JObject;
			if (message.Data is not JObject data)
			{
				SendError("Invalid message data.");
				return;
			}
			
			var deviceId = data.Value<string>("Id") ?? string.Empty;
			var favId = data.Value<string>("FavId") ?? string.Empty;
			if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(favId))
			{
				SendError("Invalid favorite POST request - missing Id or FavId");
				return;
			}

			var temp = TransportDialFavoriteRequest;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(deviceId, favId));
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.TransportComponent.HandlePostFavoriteRequest() - {0}", ex);
			SendError($"Invalid message format: {ex.Message}");
		}
	}

	private void HandlePostTransportRequest(ResponseBase message)
	{
		Logger.Debug("TransportComponent.handlePostTransportRequest()");

		try
		{
			if (message.Data is not JObject data)
			{
				SendError("Invalid message data.");
				return;
			}
			
			var deviceId = data.Value<string>("Id") ?? string.Empty;
			var tag = data.Value<string>("Tag") ?? string.Empty;

			if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(tag))
			{
				SendError("Invalid transport POST request - missing Id or Tag.");
				return;
			}

			var transport = TransportUtilities.FindTransport(tag);
			if (transport == TransportTypes.Unknown)
			{
				SendError($"Invalid transport POST requested - unsupported transport command: {tag}");
			}
			else
			{
				var temp = TransportControlRequest;
				temp?.Invoke(this, new GenericDualEventArgs<string, TransportTypes>(deviceId, transport));
			}

		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.TransportComponent.HandlePostTransportRequest() - {0}", ex);
			var errMessage = MessageFactory.CreateErrorResponse($"Invalid message format: {ex.Message}");
			Send(errMessage, ApiHooks.DeviceControl);
		}
	}

	private void HandlePostChannelRequest(ResponseBase message)
	{
		Logger.Debug("CrComLibUi.HandlePostChannelRequest()");

		try
		{
			if (message.Data is not JObject data)
			{
				SendError("Invalid message data.");
				return;
			}
			
			var deviceId = data.Value<string>("Id") ?? string.Empty;
			var channel = data.Value<string>("Chan") ?? string.Empty;
			if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(channel))
			{
				SendError("Invalid channel POst request - missing ID or Chan.");
				return;
			}

			var temp = TransportDialRequest;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(deviceId, channel));
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.TransportComponent.HandlePostChannelRequest() - {0}", ex);
			SendError($"Invalid message format: {ex.Message}");
		}
	}

	private void SendError(string message)
	{
		var errMsg = MessageFactory.CreateErrorResponse(message);
		Send(errMsg, ApiHooks.DeviceControl);
	}
}