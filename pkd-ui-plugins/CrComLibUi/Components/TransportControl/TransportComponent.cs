namespace CrComLibUi.Components.TransportControl
{
	using Crestron.SimplSharpPro.DeviceSupport;
	using pkd_application_service.Base;
	using pkd_application_service.UserInterface;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_ui_service.Interfaces;
	using pkd_ui_service.Utility;
	using CrComLibUi.Api;
	using System;
	using System.Collections.ObjectModel;
    using Newtonsoft.Json.Linq;

    internal class TransportComponent : BaseComponent, ITransportControlUserInterface
	{
		private static readonly string COMMAND_CONFIG = "CONFIG";
		private static readonly string COMMAND_TRANSPORT = "TRANSPORT";
		private static readonly string COMMAND_FAVORITE = "FAVORITE";
		private static readonly string COMMAND_CHANNEL = "CHANNEL";

		private ReadOnlyCollection<TransportInfoContainer> devices;

		public TransportComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
			: base(ui, uiData)
		{
			GetHandlers.Add(COMMAND_CONFIG, HandleGetConfigRequest);
			PostHandlers.Add(COMMAND_FAVORITE, HandlePostFavoriteRequest);
			PostHandlers.Add(COMMAND_TRANSPORT, HandlePostTransportRequest);
			PostHandlers.Add(COMMAND_CHANNEL, HandlePostChannelRequest);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, TransportTypes>> TransportControlRequest;
		
		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> TransportDialRequest;
		
		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> TransportDialFavoriteRequest;

		/// <inheritdoc/>
		public override void HandleSerialResponse(string response)
		{
			try
			{
				ResponseBase message = MessageFactory.DeserializeMessage(response);
				if (message == null)
				{
					ResponseBase errMessage = MessageFactory.CreateErrorResponse("Invalid message format.");
					Send(errMessage, ApiHooks.DeviceControl);
					return;
				}

				if (message.Method.Equals("GET"))
				{
					HandleGetRequest(message);
				}
				else if (message.Method.Equals("POST"))
				{
					HandlePostRequest(message);
				}
				else
				{
					ResponseBase errMessage = MessageFactory.CreateErrorResponse($"Unsupported method: {message.Method}.");
					Send(errMessage, ApiHooks.DeviceControl);
					return;
				}
			}
			catch (Exception ex)
			{
				Logger.Error("CrComLibUi.TransportComponent.HandleSerialResponse() - {0}", ex);
				ResponseBase errMessage = MessageFactory.CreateErrorResponse($"Invalid message format: {ex.Message}");
				Send(errMessage, ApiHooks.DeviceControl);
			}
		}

		/// <inheritdoc/>
		public override void Initialize()
		{
			Initialized = false;
			if(this.devices == null)
			{
				Logger.Error("CrComLibUi.TransportComponent.Initialize() - Call SetCableBoxData() first.");
				return;
			}

			Initialized = true;
		}

		/// <inheritdoc/>
		public void SetCableBoxData(ReadOnlyCollection<TransportInfoContainer> data)
		{
			if (data == null)
			{
				Logger.Error("CrComLibUi.TransportComponent.SetCableBoxData() - argument 'data' cannot be null.");
				return;
			}

			devices = data;
			if (Initialized)
			{
				ResponseBase rx = MessageFactory.CreateGetResponseObject();
				rx.Command = COMMAND_CONFIG;
				rx.Data = devices;
				Send(rx, ApiHooks.DeviceControl);
			}
		}

		private void HandleGetRequest(ResponseBase message)
		{
			if (GetHandlers.TryGetValue(message.Command, out var handler))
			{
				handler.Invoke(message);
			}
			else
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Unsupported GET command: {message.Command}");
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
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Unsupported POST command: {message.Command}");
				Send(errRx, ApiHooks.DeviceControl);
			}
		}

		private void HandleGetConfigRequest(ResponseBase message)
		{
			ResponseBase rx = MessageFactory.CreateGetResponseObject();
			rx.Command = COMMAND_CONFIG;
			rx.Data = devices;
			Send(rx, ApiHooks.DeviceControl);
		}

		private void HandlePostFavoriteRequest(ResponseBase message)
		{
			Logger.Debug("TransportComponent.HandlePostFavoriteRequest()");

			try
			{
				JObject data = message.Data as JObject;
				string deviceId = data?.Value<string>("Id") ?? string.Empty;
				string favId = data?.Value<string>("FavId") ?? string.Empty;

				if (deviceId == string.Empty || favId == string.Empty)
				{
					SendError($"CrComLibUi.TransportComponent.HandlePostFavoriteRequest() - Invalid data package received.");
					return;
				}

				var temp = this.TransportDialFavoriteRequest;
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
                JObject data = message.Data as JObject;
                string deviceId = data?.Value<string>("Id") ?? string.Empty;
                string tag = data?.Value<string>("Tag") ?? string.Empty;

                if (deviceId == string.Empty || tag == string.Empty)
                {
                    SendError($"Invalid data package received.");
                    return;
                }

                TransportTypes transport = TransportUtilities.FindTransport(tag);
				if (transport == TransportTypes.Unknown)
				{
					SendError($"CrComLibUi.TransportComponent.HandlePostTransportRequest() - Unsupported transport command: {tag}");
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
				ResponseBase errMessage = MessageFactory.CreateErrorResponse($"Invalid message format: {ex.Message}");
				Send(errMessage, ApiHooks.DeviceControl);
			}
		}

		private void HandlePostChannelRequest(ResponseBase message)
		{
			Logger.Debug("CrComLibUi.HandlePostChannelRequest()");

			try
			{
                JObject data = message.Data as JObject;
                string deviceId = data?.Value<string>("Id") ?? string.Empty;
                string channel = data?.Value<string>("Chan") ?? string.Empty;
                if (deviceId == string.Empty || channel == string.Empty)
                {
                    SendError($"CrComLibUi.TransportComponent..HandlePostChannelRequest() - Invalid data package received.");
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
			ResponseBase errMsg = MessageFactory.CreateErrorResponse(message);
			Send(errMsg, ApiHooks.DeviceControl);
		}
	}
}
