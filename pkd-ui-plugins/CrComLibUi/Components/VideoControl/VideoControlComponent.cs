namespace CrComLibUi.Components.VideoControl
{
	using Crestron.SimplSharpPro.DeviceSupport;
	using pkd_application_service.AvRouting;
	using pkd_application_service.Base;
	using pkd_application_service.UserInterface;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_ui_service.Interfaces;
	using CrComLibUi.Api;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Dynamic;
	using System.Linq;

	internal class VideoControlComponent : BaseComponent, IRoutingUserInterface
	{
		private static readonly string COMMAND_CONFIG = "CONFIG";
		private static readonly string COMMAND_ROUTE = "ROUTE";
		private static readonly string COMMAND_FREEZE = "GLOBALFREEZE";
		private static readonly string COMMAND_BLANK = "GLOBALBLANK";
		private ReadOnlyCollection<AvSourceInfoContainer> sources;
		private List<VideoDestinationInfo> destinations;
		private ReadOnlyCollection<InfoContainer> avrs;
		private bool globalBlankState;
		private bool globalFreezeState;

		public VideoControlComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
			: base(ui, uiData)
		{
			GetHandlers.Add(COMMAND_CONFIG, HandleGetConfigRequest);
			PostHandlers.Add(COMMAND_ROUTE, HandlePostRouteRequest);
			PostHandlers.Add(COMMAND_BLANK, HandlePostBlankRequest);
			PostHandlers.Add(COMMAND_FREEZE, HandlePostFreezeRequest);
		}

		public event EventHandler<GenericDualEventArgs<string, string>> AvRouteChangeRequest;
		public event EventHandler GlobalFreezeToggleRequest;
		public event EventHandler GlobalBlankToggleRequest;
		
		/// <inheritdoc/>
		public override void HandleSerialResponse(string response)
		{
			if (!this.CheckInitialized(
				"VideoControlComponent",
				nameof(HandleSerialResponse))) return;

			try
			{
				ResponseBase message = MessageFactory.DeserializeMessage(response);
				if (message == null)
				{
					ResponseBase errMessage = MessageFactory.CreateErrorResponse("Invalid message format.");
					Send(errMessage, ApiHooks.RoomConfig);
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
					Send(errMessage, ApiHooks.VideoControl);
					return;
				}
			}
			catch (Exception ex)
			{
				Logger.Error("GcuVueUi.VideoControlComponent.HandleSerialResponse() - {0}", ex);
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Failed to parse message: {ex.Message}");
				Send(errRx, ApiHooks.VideoControl);
			}
		}

		/// <inheritdoc/>
		public override void Initialize()
		{
			Initialized = false;
			if (uiData == null)
			{
				Logger.Error("GcuVueUi.VideoControlComponent.Initialize() - Set UiData first.");
				return;
			}

			if (sources == null || destinations == null)
			{
				Logger.Error("GcuVueUi.VideoControlComponent.Initialize() - set source and destination data first (call SetRoutingData()).");
				return;
			}

			Initialized = true;
		}

		/// <inheritdoc/>
		public void SetRoutingData(
			ReadOnlyCollection<AvSourceInfoContainer> sources,
			ReadOnlyCollection<InfoContainer> destinations,
			ReadOnlyCollection<InfoContainer> avRouters)
		{
			Logger.Debug("VideoControlComponent.SetRoutingData()");

			if (sources == null)
			{
				Logger.Error("GcuVueUi.VideoControlComponent.SetRoutingData() - argument 'sources' cannot be null.");
				return;
			}

			if (destinations == null)
			{
				Logger.Error("GcuVueUi.VideoControlComponent.SetRoutingData() - argument 'destinations' cannot be null.");
				return;
			}

			if (avRouters == null)
			{
				Logger.Error("GcuVueUi.VideoControlComponent.SetRoutingData() - argument 'avRouters' cannot be null.");
				return;
			}


			this.sources = sources;
			this.avrs = avRouters;
			this.destinations = new List<VideoDestinationInfo>();
			foreach (var dest in destinations)
			{
				this.destinations.Add(new VideoDestinationInfo(dest.Id, dest.Label, dest.Icon, dest.Tags)
				{
					CurrentSourceId = string.Empty,
				});
			}
		}

		public void SetGlobalBlankState(bool state)
		{
			globalBlankState = state;
			ResponseBase message = MessageFactory.CreateGetResponseObject();
			message.Command = COMMAND_BLANK;
			message.Data = globalBlankState;
			Send(message, ApiHooks.VideoControl);
		}

		public void SetGlobalFreezeState(bool state)
		{
			globalFreezeState = state;
			ResponseBase message = MessageFactory.CreateGetResponseObject();
			message.Command = COMMAND_FREEZE;
			message.Data = globalFreezeState;
			Send (message, ApiHooks.VideoControl);
		}

		/// <inheritdoc/>
		public void UpdateAvRoute(AvSourceInfoContainer inputInfo, string outputId)
		{
			if (!this.CheckInitialized(
				"VideoControlComponent",
				nameof(UpdateAvRoute))) return;

			var dest = destinations.FirstOrDefault(x => x.Id == outputId);
			if (dest == null)
			{
				Logger.Error("GcuVueUi.VideoControlComponent.UpdateAvRoute() - No destination with id {0}", outputId);
				return;
			}

			dest.CurrentSourceId = inputInfo.Id;
			ResponseBase message = MessageFactory.CreateGetResponseObject();
			message.Command = COMMAND_ROUTE;

			dynamic dataObj = new ExpandoObject();
			dataObj.DestId = dest.Id;
			dataObj.SrcId = inputInfo.Id;
			message.Data = dataObj;
			Send(message, ApiHooks.VideoControl);
		}

		private void HandleGetRequest(ResponseBase response)
		{
			if (GetHandlers.TryGetValue(response.Command, out var handler))
			{
				handler.Invoke(response);
			}
			else
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Unsupported GET command: {response.Command}");
				Send(errRx, ApiHooks.VideoControl);
			}
		}

		private void HandlePostRequest(ResponseBase response)
		{
			if (PostHandlers.TryGetValue(response.Command, out var handler))
			{
				handler.Invoke(response);
			}
			else
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Unsupported POST command: {response.Command}");
				Send(errRx, ApiHooks.VideoControl);
			}
		}

		private void HandleGetConfigRequest(ResponseBase response)
		{
			try
			{
				ResponseBase message = MessageFactory.CreateGetResponseObject();
				message.Command = COMMAND_CONFIG;
				message.Data = CreateConfigData();
				Send(message, ApiHooks.VideoControl);
			}
			catch (Exception ex)
			{
				Logger.Error("GcuVueUi.VideoControlComponent.HandleGetConfigRequest() - {0}", ex);
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Failed to parse config request: {ex.Message}");
				Send(errRx, ApiHooks.VideoControl);
			}
		}

		private void HandlePostRouteRequest(ResponseBase response)
		{
			try
			{
				var temp = AvRouteChangeRequest;
				temp?.Invoke(this, new GenericDualEventArgs<string, string>(response.Data.SrcId, response.Data.DestId));
			}
			catch (Exception ex)
			{
				Logger.Error("GcuVueUi.VideoControlComponent.HandlePostRouteRequest() - {0}", ex);
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Failed to parse route POST request: {ex.Message}");
				Send(errRx, ApiHooks.VideoControl);
			}
		}

		private void HandlePostFreezeRequest(ResponseBase response)
		{
			var temp = GlobalFreezeToggleRequest;
			temp?.Invoke(this, EventArgs.Empty);
		}

		private void HandlePostBlankRequest(ResponseBase response)
		{
			var temp = GlobalBlankToggleRequest;
			temp?.Invoke(this, EventArgs.Empty);
		}

		private VideoConfigData CreateConfigData()
		{
			VideoConfigData config = new VideoConfigData()
			{
				Blank = globalBlankState,
				Freeze = globalFreezeState,
			};

			foreach (var avr in avrs)
			{
				config.AvRouters.Add(new AvRouter()
				{
					Id = avr.Id,
					Label = avr.Label,
					IsOnline = avr.IsOnline,
				});
			}
			foreach (var source in sources)
			{
				config.Sources.Add(new VideoSource()
				{
					Icon = source.Icon,
					Id = source.Id,
					Label = source.Label,
					Control = source.ControlId,
					HasSync = true,
					Tags = source.Tags,
				});
			}

			foreach (var dest in destinations)
			{
				config.Destinations.Add(new VideoDestination()
				{
					Icon = dest.Icon,
					Id = dest.Id,
					Label = dest.Label,
					CurrentSourceId = dest.CurrentSourceId,
					Tags = dest.Tags,
				});
			}

			return config;
		}
	}
}
