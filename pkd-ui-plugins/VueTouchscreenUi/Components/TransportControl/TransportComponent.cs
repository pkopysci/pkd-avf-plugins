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
				Logger.Error("GcuVueUi.TransportComponent.HandleSerialResponse() - {0}", ex);
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
				Logger.Error("GcuVueUi.TransportComponent.Initialize() - Call SetCableBoxData() first.");
				return;
			}

			Initialized = true;
		}

		/// <inheritdoc/>
		public void SetCableBoxData(ReadOnlyCollection<TransportInfoContainer> data)
		{
			if (data == null)
			{
				Logger.Error("GcuVueUi.TransportComponent.SetCableBoxData() - argument 'data' cannot be null.");
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
				var temp = this.TransportDialFavoriteRequest;
				temp?.Invoke(this, new GenericDualEventArgs<string, string>(message.Data.Id, message.Data.FavId));
			}
			catch (Exception ex)
			{
				Logger.Error("GcuVueUi.TransportComponent.HandlePostFavoriteRequest() - {0}", ex);
				ResponseBase errMessage = MessageFactory.CreateErrorResponse($"Invalid message format: {ex.Message}");
				Send(errMessage, ApiHooks.DeviceControl);
			}
		}

		private void HandlePostTransportRequest(ResponseBase message)
		{
			Logger.Debug("TransportComponent.handlePostTransportRequest()");

			try
			{
				TransportTypes transport = TransportUtilities.FindTransport(message.Data.Tag);
				if (transport == TransportTypes.Unknown)
				{
					ResponseBase errMessage = MessageFactory.CreateErrorResponse($"Unsupported transport command: {message.Data.Tag}");
					Send(errMessage, ApiHooks.DeviceControl);
				}
				else
				{
					var temp = TransportControlRequest;
					temp?.Invoke(this, new GenericDualEventArgs<string, TransportTypes>(message.Data.Id, transport));
				}

			}
			catch (Exception ex)
			{
				Logger.Error("GcuVueUi.TransportComponent.HandlePostTransportRequest() - {0}", ex);
				ResponseBase errMessage = MessageFactory.CreateErrorResponse($"Invalid message format: {ex.Message}");
				Send(errMessage, ApiHooks.DeviceControl);
			}
		}

		private void HandlePostChannelRequest(ResponseBase message)
		{
			try
			{
				var temp = TransportDialRequest;
				temp?.Invoke(this, new GenericDualEventArgs<string, string>(message.Data.Id, message.Data.Chan));
			}
			catch (Exception ex)
			{
				Logger.Error("GcuVueUi.TransportComponent.HandlePostChannelRequest() - {0}", ex);
				ResponseBase errorMessage = MessageFactory.CreateErrorResponse($"Invalid message format: {ex.Message}");
				Send(errorMessage, ApiHooks.DeviceControl);
			}
		}
	}
}
