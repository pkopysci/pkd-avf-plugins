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
		rx.Data["Devices"] = JToken.FromObject(_devices);
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
		rx.Data["Devices"] = JToken.FromObject(_devices);
		Send(rx, ApiHooks.DeviceControl);
	}

	private void HandlePostFavoriteRequest(ResponseBase message)
	{
		Logger.Debug("TransportComponent.HandlePostFavoriteRequest()");
		
		try
		{
			var deviceId = message.Data.Value<string>("Id");
			var favoriteId = message.Data.Value<string>("FavId");
			if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(favoriteId))
			{
				SendError("Invalid POST favorite request - missing Id or FavId.", ApiHooks.DeviceControl);
				return;
			}
			
			var device = _devices.FirstOrDefault(x => x.Id == deviceId);
			if (device == null)
			{
				SendError($"Invalid POST favorite request - No device with id {deviceId}", ApiHooks.DeviceControl);
				return;
			}

			if (device.Favorites.All(x => x.Id != favoriteId))
			{
				SendError(
					$"Invalid POST favorite reques - device {deviceId} does not have favorite with id {favoriteId}",
					ApiHooks.DeviceControl);
				return;
			}

			var temp = TransportDialFavoriteRequest;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(deviceId, favoriteId));
		}
		catch (Exception e)
		{
			Logger.Error("CrComLibUi.TransportComponent.HandlePostFavoriteRequest() - {0}", e.Message);
			SendServerError(ApiHooks.DeviceControl);
		}
	}

	private void HandlePostTransportRequest(ResponseBase message)
	{
		Logger.Debug("TransportComponent.handlePostTransportRequest()");
		try
		{
			var deviceId = message.Data.Value<string>("Id");
			var tag = message.Data.Value<string>("Tag");
			if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(tag))
			{
				SendError("Invalid POST transport request - missing Id or Tag.", ApiHooks.DeviceControl);
				return;
			}
			
			var device = _devices.FirstOrDefault(x => x.Id == deviceId);
			if (device == null)
			{
				SendError($"Invalid POST transport request - no device with id {deviceId}", ApiHooks.DeviceControl);
				return;
			}
			
			var transport = TransportUtilities.FindTransport(tag);
			if (transport == TransportTypes.Unknown)
			{
				SendError($"Invalid POST transport request - no command with tag {tag}", ApiHooks.DeviceControl);
				return;
			}

			var temp = TransportControlRequest;
			temp?.Invoke(this, new GenericDualEventArgs<string, TransportTypes>(deviceId, transport));
		}
		catch (Exception e)
		{
			Logger.Error("CrComLibUi.TransportComponent.HandlePostTransportRequest() - {0}", e.Message);
			SendServerError(ApiHooks.DeviceControl);
		}
	}

	private void HandlePostChannelRequest(ResponseBase message)
	{
		Logger.Debug("CrComLibUi.HandlePostChannelRequest()");
		try
		{
			var deviceId = message.Data.Value<string>("Id");
			var channel = message.Data.Value<string>("Chan");
			if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(channel))
			{
				SendError("Invalid POST channel request - missing Id or Channel.", ApiHooks.DeviceControl);
				return;
			}

			if (_devices.All(x => x.Id != deviceId))
			{
				SendError($"Invalid POST channel request - no device with id {deviceId}", ApiHooks.DeviceControl);
				return;
			}

			var temp = TransportDialRequest;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(deviceId, channel));
		}
		catch (Exception e)
		{
			Logger.Error("CrComLibUi.TransportComponent.HandlePostChannelRequest() - {0}", e.Message);
			SendServerError(ApiHooks.DeviceControl);
		}
	}
}