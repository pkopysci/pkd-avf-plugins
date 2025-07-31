using Newtonsoft.Json.Linq;
using pkd_application_service;

namespace CrComLibUi.Components.VideoControl;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Linq;
using Api;
using Crestron.SimplSharpPro.DeviceSupport;
using pkd_application_service.AvRouting;
using pkd_application_service.Base;
using pkd_application_service.UserInterface;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;

internal class VideoControlComponent(
	BasicTriListWithSmartObject ui,
	UserInterfaceDataContainer uiData,
	IAvRoutingApp appService)
	: BaseComponent(ui, uiData)
{
	private const string CommandConfig = "CONFIG";
	private const string CommandRoute = "ROUTE";
	private const string CommandFreeze = "GLOBALFREEZE";
	private const string CommandBlank = "GLOBALBLANK";
	private const string CommandStatus = "STATUS";
	private const string CommandInput = "INPUT";
	private readonly List<VideoDestination> _destinations = [];
	private readonly List<VideoSource> _sources = [];
	private ReadOnlyCollection<InfoContainer> _routers = new([]);
	private bool _globalBlankState;
	private bool _globalFreezeState;
		
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
		
		SetRoutingData(appService.GetAllAvSources(), appService.GetAllAvDestinations(), appService.GetAllAvRouters());
		
		GetHandlers.Add(CommandConfig, HandleGetConfigRequest);
		PostHandlers.Add(CommandRoute, HandlePostRouteRequest);
		PostHandlers.Add(CommandBlank, HandlePostBlankRequest);
		PostHandlers.Add(CommandFreeze, HandlePostFreezeRequest);
		
		appService.RouteChanged += AppServiceOnRouteChanged;
		appService.RouterConnectChange += AppServiceOnRouterConnectChange;
		appService.VideoInputSyncChanged += AppServiceOnInputSyncChanged;
		if (appService is IApplicationService genericApp)
		{
			genericApp.GlobalVideoBlankChanged += GenericAppOnGlobalVideoBlankChanged;
			genericApp.GlobalVideoFreezeChanged += GenericAppOnGlobalVideoFreezeChanged;
		}
		
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

	private void GenericAppOnGlobalVideoFreezeChanged(object? sender, EventArgs e)
	{
		if (appService is not IApplicationService genericApp) return;
		_globalFreezeState = genericApp.QueryGlobalVideoFreeze();
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandFreeze;
		message.Data.Add(new JProperty("State", _globalFreezeState));
		Send (message, ApiHooks.VideoControl);
	}

	private void GenericAppOnGlobalVideoBlankChanged(object? sender, EventArgs e)
	{
		if (appService is not IApplicationService genericApp) return;
		_globalBlankState = genericApp.QueryGlobalVideoBlank();
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandBlank;
		message.Data.Add(new JProperty("State", _globalBlankState));
		Send(message, ApiHooks.VideoControl);
	}

	private void AppServiceOnRouterConnectChange(object? sender, GenericSingleEventArgs<string> e)
	{
		var found = _routers.FirstOrDefault(x => x.Id == e.Arg);
		if (found == null)
		{
			Logger.Error($"CrComLibUi.VideoControlComponent.UpdateAvRouterConnectionStatus() - {e.Arg} not found.");
			return;
		}
		
		found.IsOnline = appService.QueryRouterConnectionStatus(e.Arg);
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandStatus;
		message.Data["Avr"] = JObject.FromObject(found);
		Send(message, ApiHooks.VideoControl);
	}

	private void AppServiceOnRouteChanged(object? sender, GenericSingleEventArgs<string> e)
	{
		var dest = _destinations.FirstOrDefault(x => x.Id == e.Arg);
		if (dest == null)
		{
			Logger.Error("CrComLibUi.VideoControlComponent.UpdateAvRoute() - No destination with id {0}", e.Arg);
			return;
		}

		var currentSource = appService.QueryCurrentRoute(e.Arg);
		dest.CurrentSourceId = currentSource.Id;
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandRoute;

		dynamic dataObj = new ExpandoObject();
		dataObj.DestId = dest.Id;
		dataObj.SrcId = currentSource.Id;
		message.Data = JObject.FromObject(dataObj);
		Send(message, ApiHooks.VideoControl);
	}

	private void AppServiceOnInputSyncChanged(object? sender, GenericSingleEventArgs<string> e)
	{
		var source = _sources.FirstOrDefault(x => x.Id.Equals(e.Arg));
		if (source == null) return;
		source.HasSync = appService.QueryVideoInputSyncStatus(e.Arg);

		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandInput;
		message.Data["Input"] = JObject.FromObject(source);
		Send(message, ApiHooks.VideoControl);
	}
	
	private void SetRoutingData(
		ReadOnlyCollection<AvSourceInfoContainer> sources,
		ReadOnlyCollection<InfoContainer> destinations,
		ReadOnlyCollection<InfoContainer> avRouters)
	{
		_routers = avRouters;
		
		_sources.Clear();
		_destinations.Clear();
		
		foreach (var source in sources)
		{
			_sources.Add(new VideoSource()
			{
				Icon = source.Icon,
				Id = source.Id,
				Label = source.Label,
				Control = source.ControlId,
				SupportsSync = source.SupportSync,
				HasSync = source.HasSync,
				Tags = source.Tags,
			});
		}
		
		foreach (var dest in destinations)
		{
			_destinations.Add(new VideoDestination()
			{
				Icon = dest.Icon,
				Id = dest.Id,
				Label = dest.Label,
				CurrentSourceId = string.Empty,
				Tags = dest.Tags,
			});
		}
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
			message.Data = JObject.FromObject(CreateConfigData());
			Send(message, ApiHooks.VideoControl);
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.VideoControlComponent.HandleGetConfigRequest() - {0}", ex);
			SendServerError(ApiHooks.VideoControl);
		}
	}

	private void HandlePostRouteRequest(ResponseBase response)
	{
		try
		{
			var srcId = response.Data.Value<string>("SrcId");
			var destId = response.Data.Value<string>("DestId");
			if (string.IsNullOrEmpty(srcId) || string.IsNullOrEmpty(destId))
			{
				Send(
				MessageFactory.CreateErrorResponse("Invalid route POST request - Missing SrcId or DestId"),
				ApiHooks.VideoControl);
				return;
			}
			
			appService.MakeRoute(srcId, destId);
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUi.VideoControlComponent.HandlePostRouteRequest() - {0}", ex);
			SendServerError(ApiHooks.VideoControl);
		}
	}

	private void HandlePostFreezeRequest(ResponseBase response)
	{
		if (appService is not IApplicationService genericApp) return;
		var currentState = genericApp.QueryGlobalVideoFreeze();
		genericApp.SetGlobalVideoFreeze(!currentState);
	}

	private void HandlePostBlankRequest(ResponseBase response)
	{
		if (appService is not IApplicationService genericApp) return;
		var currentState = genericApp.QueryGlobalVideoBlank();
		genericApp.SetGlobalVideoBlank(!currentState);
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
				Model =  avr.Model,
				Label = avr.Label,
				IsOnline = avr.IsOnline,
			});
		}

		config.Sources = _sources;
		config.Destinations = _destinations;
		return config;
	}
}