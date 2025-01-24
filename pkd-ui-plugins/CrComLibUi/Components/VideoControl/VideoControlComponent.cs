namespace CrComLibUi.Components.VideoControl;

using Api;
using Crestron.SimplSharpPro.DeviceSupport;
using pkd_application_service.AvRouting;
using pkd_application_service.Base;
using pkd_application_service.UserInterface;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Linq;
using Newtonsoft.Json.Linq;

internal class VideoControlComponent : BaseComponent, IRoutingUserInterface
{
	private const string CommandConfig = "CONFIG";
	private const string CommandRoute = "ROUTE";
	private const string CommandFreeze = "GLOBALFREEZE";
	private const string CommandBlank = "GLOBALBLANK";
	private readonly List<VideoDestinationInfo> _destinations;
	private ReadOnlyCollection<AvSourceInfoContainer> _sources;
	private ReadOnlyCollection<InfoContainer> _routers;
	private bool _globalBlankState;
	private bool _globalFreezeState;

	public VideoControlComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
		: base(ui, uiData)
	{
		GetHandlers.Add(CommandConfig, HandleGetConfigRequest);
		PostHandlers.Add(CommandRoute, HandlePostRouteRequest);
		PostHandlers.Add(CommandBlank, HandlePostBlankRequest);
		PostHandlers.Add(CommandFreeze, HandlePostFreezeRequest);
		_sources = new ReadOnlyCollection<AvSourceInfoContainer>([]);
		_routers = new ReadOnlyCollection<InfoContainer>([]);
		_destinations = [];
	}

	public event EventHandler<GenericDualEventArgs<string, string>>? AvRouteChangeRequest;
	public event EventHandler? GlobalFreezeToggleRequest;
	public event EventHandler? GlobalBlankToggleRequest;
		
	/// <inheritdoc/>
	public override void HandleSerialResponse(string response)
	{
		if (!CheckInitialized(
			    "VideoControlComponent",
			    nameof(HandleSerialResponse))) return;

		try
		{
			var message = MessageFactory.DeserializeMessage(response);
			if (string.IsNullOrEmpty(message.Method))
			{
				var errMessage = MessageFactory.CreateErrorResponse("Invalid message format.");
				Send(errMessage, ApiHooks.RoomConfig);
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
					Send(errMessage, ApiHooks.VideoControl);
					return;
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.VideoControlComponent.HandleSerialResponse() - {0}", ex);
			var errRx = MessageFactory.CreateErrorResponse("500 - Internal Server Error.");
			Send(errRx, ApiHooks.VideoControl);
		}
	}

	/// <inheritdoc/>
	public override void Initialize()
	{
		Initialized = false;
		if (_sources.Count == 0 || _destinations.Count == 0)
		{
			Logger.Debug("CrComLibUi.VideoControlComponent.Initialize() - sources or destinations are empty.");
			return;
		}
		Initialized = true;
	}

	public override void SendConfig()
	{
		HandleGetConfigRequest(MessageFactory.CreateGetResponseObject());
	}

	/// <inheritdoc/>
	public void SetRoutingData(
		ReadOnlyCollection<AvSourceInfoContainer> sources,
		ReadOnlyCollection<InfoContainer> destinations,
		ReadOnlyCollection<InfoContainer> avRouters)
	{
		_sources = sources;
		_routers = avRouters;
		_destinations.Clear();
		foreach (var dest in destinations)
		{
			_destinations.Add(new VideoDestinationInfo(dest.Id, dest.Label, dest.Icon, dest.Tags)
			{
				CurrentSourceId = string.Empty,
			});
		}
	}

	public void SetGlobalBlankState(bool state)
	{
		_globalBlankState = state;
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandBlank;
		message.Data = _globalBlankState;
		Send(message, ApiHooks.VideoControl);
	}

	public void SetGlobalFreezeState(bool state)
	{
		_globalFreezeState = state;
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandFreeze;
		message.Data = _globalFreezeState;
		Send (message, ApiHooks.VideoControl);
	}

	/// <inheritdoc/>
	public void UpdateAvRoute(AvSourceInfoContainer inputInfo, string outputId)
	{
		if (!CheckInitialized("VideoControlComponent", nameof(UpdateAvRoute))) return;

		var dest = _destinations.FirstOrDefault(x => x.Id == outputId);
		if (dest == null)
		{
			Logger.Error("CrComLibUi.VideoControlComponent.UpdateAvRoute() - No destination with id {0}", outputId);
			return;
		}

		dest.CurrentSourceId = inputInfo.Id;
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandRoute;

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
			var errRx = MessageFactory.CreateErrorResponse($"Unsupported GET command: {response.Command}");
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
			var errRx = MessageFactory.CreateErrorResponse($"Unsupported POST command: {response.Command}");
			Send(errRx, ApiHooks.VideoControl);
		}
	}

	private void HandleGetConfigRequest(ResponseBase response)
	{
		try
		{
			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CommandConfig;
			message.Data = CreateConfigData();
			Send(message, ApiHooks.VideoControl);
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.VideoControlComponent.HandleGetConfigRequest() - {0}", ex);
			var errRx = MessageFactory.CreateErrorResponse("500 - Internal Server Error.");
			Send(errRx, ApiHooks.VideoControl);
		}
	}

	private void HandlePostRouteRequest(ResponseBase response)
	{
		var temp = AvRouteChangeRequest;
		if (temp == null) return;

		try
		{
			if (response.Data is not JObject data)
			{
				Send(
					MessageFactory.CreateErrorResponse("Invalid route POST request - Data is not valid JSON."),
					ApiHooks.VideoControl);
				return;
			}
			
			var srcId = data.Value<string>("SrcId");
			var destId = data.Value<string>("DestId");
			if (string.IsNullOrEmpty(srcId) || string.IsNullOrEmpty(destId))
			{
				Send(
					MessageFactory.CreateErrorResponse("Invalid route POST request - Missing SrcId or DestId"),
					ApiHooks.VideoControl);
				return;
			}
			
			temp.Invoke(this, new GenericDualEventArgs<string, string>(srcId, destId));
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.VideoControlComponent.HandlePostRouteRequest() - {0}", ex);
			var errRx = MessageFactory.CreateErrorResponse("500 - Internal Server Error.");
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
		var config = new VideoConfigData()
		{
			Blank = _globalBlankState,
			Freeze = _globalFreezeState,
		};

		foreach (var avr in _routers)
		{
			config.AvRouters.Add(new AvRouter()
			{
				Id = avr.Id,
				Label = avr.Label,
				IsOnline = avr.IsOnline,
			});
		}
		foreach (var source in _sources)
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

		foreach (var dest in _destinations)
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