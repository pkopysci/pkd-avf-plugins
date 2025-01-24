namespace CrComLibUi.Components.Lighting;

using Crestron.SimplSharpPro.DeviceSupport;
using pkd_application_service.LightingControl;
using pkd_application_service.UserInterface;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_ui_service.Interfaces;
using Api;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Linq;
using Newtonsoft.Json.Linq;


internal class LightingComponent : BaseComponent, ILightingUserInterface
{
	private const string CommandConfig = "CONFIG";
	private const string CommandZone = "LOAD";
	private const string CommandScene = "SCENE";
	private List<LightingData> _lights;

	public LightingComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
		: base(ui, uiData)
	{
		GetHandlers.Add(CommandConfig, GetConfigHandler);
		GetHandlers.Add(CommandScene, GetSceneHandler);
		GetHandlers.Add(CommandZone, GetZoneHandler);
		PostHandlers.Add(CommandZone, PostZoneHandler);
		PostHandlers.Add(CommandScene, PostSceneHandler);
		_lights = [];
	}

	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, string>>? LightingSceneRecallRequest;
	/// <inheritdoc/>
	public event EventHandler<GenericTrippleEventArgs<string, string, int>>? LightingLoadChangeRequest;

	/// <inheritdoc/>
	public override void HandleSerialResponse(string response)
	{
		try
		{
			var message = MessageFactory.DeserializeMessage(response);
			if (string.IsNullOrEmpty(message.Method))
			{
				var errMsg = MessageFactory.CreateErrorResponse("Invalid message format.");
				Send(errMsg, ApiHooks.LightingControl);
				return;
			}

			switch (message.Method)
			{
				case "GET":
					HandleGetRequests(message);
					break;
				case "POST":
					HandlePostRequests(message);
					break;
				default:
				{
					var errMessage = MessageFactory.CreateErrorResponse($"Unsupported method: {message.Method}.");
					Send(errMessage, ApiHooks.LightingControl);
					return;
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error("CrComLibUI.LightingComponent.HandleSerialResponse() - {0}", ex);
			var errMessage = MessageFactory.CreateErrorResponse("500 - Internal Server Error.");
			Send(errMessage, ApiHooks.LightingControl);
		}
	}

	/// <inheritdoc/>
	public override void Initialize()
	{
		Logger.Debug("CrComLibUI.LightingComponent.Initialize()");
			
		Initialized = false;
		if (_lights.Count == 0)
			Logger.Warn("CrComLibUI.LightingComponent.Initialize() - No lighting data has been added.");
		Initialized = true;
	}

	/// <inheritdoc/>
	public void SetLightingData(ReadOnlyCollection<LightingControlInfoContainer> lightingData)
	{
		_lights = [];
		foreach (var controller in lightingData)
		{
			List<LightingSceneData> scenes = [];
			foreach (var scene in controller.Scenes)
			{
				scenes.Add(new LightingSceneData()
				{
					Id = scene.Id,
					Label = scene.Label,
					Tags = scene.Tags,
				});
			}

			List<LightingZoneData> zones = [];
			foreach (var zone in controller.Zones)
			{
				zones.Add(new LightingZoneData()
				{
					Id = zone.Id,
					Label = zone.Label,
					Tags = zone.Tags,
				});
			}

			var data = new LightingData()
			{
				Id = controller.Id,
				Label = controller.Label,
				Tags = controller.Tags,
				Scenes = scenes,
				Zones = zones
			};

			_lights.Add(data);
		}
	}

	/// <inheritdoc/>
	public void UpdateActiveLightingScene(string controlId, string sceneId)
	{
		var control = _lights.FirstOrDefault(x => x.Id == controlId);
		if (control == null)
		{
			Logger.Error("CrComLibUi.LightingComponent.UpdateActiveLightingScene() - No controller with ID {0}", controlId);
			return;
		}

		foreach (var scene in control.Scenes)
		{
			scene.Set = scene.Id.Equals(sceneId);
		}

		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandScene;
		message.Data = new ExpandoObject();
		message.Data.Id = controlId;
		message.Data.SceneId = sceneId;
		message.Data.Set = true;

		Logger.Debug($"CrComLibUi.LightingComponent.UpdateActiveLightingScene() - sending message: {message}");

		Send(message, ApiHooks.LightingControl);
	}

	/// <inheritdoc/>
	public void UpdateLightingZoneLoad(string controlId, string zoneId, int level)
	{
		var control = _lights.FirstOrDefault(x => x.Id == controlId);
		if (control == null)
		{
			Logger.Error("CrComLibUI.LightingComponent.UpdateActiveLightingScene() - No controller with ID {0}", controlId);
			return;
		}

		foreach (var zone in control.Zones)
		{
			if (!zone.Id.Equals(zoneId)) continue;
			zone.Load = level;
			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CommandZone;
			message.Data = new ExpandoObject();
			message.Data.Id = controlId;
			message.Data.ZoneId = zoneId;
			message.Data.Load = true;
			Send(message, ApiHooks.LightingControl);
			break;
		}
	}

	private LightingData? FindController(string id, string command)
	{
		var controller = _lights.FirstOrDefault(x => x.Id.Equals(id));
		if (controller == null)
		{
			var errRx = MessageFactory.CreateErrorResponse($"No light control with ID {id}.");
			errRx.Command = command;
			Send(errRx, ApiHooks.LightingControl);
		}

		return controller;
	}

	private void HandleGetRequests(ResponseBase response)
	{
		if (GetHandlers.TryGetValue(response.Command, out var handler))
		{
			handler.Invoke(response);
		}
		else
		{
			SendError($"Unsupported GET command: {response.Command}");
		}
	}

	private void HandlePostRequests(ResponseBase response)
	{
		if (PostHandlers.TryGetValue(response.Command,out var handler))
		{
			handler.Invoke(response);
		}
		else
		{
			SendError($"Unsupported POST command: {response.Command}");
		}
	}

	private void GetConfigHandler(ResponseBase response)
	{
		response.Data = _lights;
		Send(response, ApiHooks.LightingControl);
	}

	private void GetZoneHandler(ResponseBase response)
	{
		try
		{
			if (response.Data is not JObject data)
			{
				SendError($"Invalid data type. { response.Data.GetType() }");
				return;
			}
				
			var controlId = data.Value<string>("Id") ?? string.Empty;
			var zoneId = data.Value<string>("ZoneId") ?? string.Empty;
			if (string.IsNullOrEmpty(zoneId) || string.IsNullOrEmpty(controlId))
			{
				SendError("Invalid zone GET request: missing controlId or zoneId.");
				return;
			}

			var controller = FindController(controlId, CommandZone);
			if (controller == null)
			{
				SendError("Unknown controller ID received.");
				return;
			}

			var zone = controller.Zones.FirstOrDefault(x => x.Id.Equals(zoneId));
			if (zone == null)
			{
				SendError($"unknown zone ID received for controller {controlId}");
				return;
			}

			response.Data = zone;
			Send(response, ApiHooks.LightingControl);
		}
		catch (Exception ex)
		{
			SendError($"Invalid GET zone command: {ex.Message}");
		}
	}

	private void GetSceneHandler(ResponseBase response)
	{
		try
		{
			if (response.Data is not JObject data)
			{
				SendError($"Scene GET requests - Invalid data type. { response.Data.GetType() }");
				return;
			}
				
			var controlId = data.Value<string>("Id") ?? string.Empty;
			var sceneId = data.Value<string>("SceneId") ?? string.Empty;
			if (string.IsNullOrEmpty(sceneId) || string.IsNullOrEmpty(controlId))
			{
				SendError("Invalid scene GET request: missing controlId or sceneId.");
				return;
			}
				
			var controller = FindController(controlId, CommandScene);
			if (controller == null)
			{
				SendError( "Unknown control ID received.");
				return;
			}

			var scene = controller.Scenes.FirstOrDefault(x => x.Id.Equals(response.Data.SceneId));
			if (scene == null)
			{
				SendError($"Control {controlId} does not have a scene with ID {sceneId}.");
				return;
			}

			response.Data = scene;
			Send(response, ApiHooks.LightingControl);
		}
		catch (Exception ex)
		{
			SendError( $"Invalid GET scene request: {ex.Message}");
		}
	}

	private void PostZoneHandler(ResponseBase response)
	{
		try
		{
			if (response.Data is not JObject data)
			{
				SendError($"zone POST requests - Invalid data type. { response.Data.GetType() }");
				return;
			}
				
			var controlId = data.Value<string>("Id") ?? string.Empty;
			var zoneId = data.Value<string>("ZoneId") ?? string.Empty;
			var load = data.Value<int>("Load");

			if (string.IsNullOrEmpty(controlId) || string.IsNullOrEmpty(zoneId))
			{
				SendError("Invalid zone POST request: missing controlId or zoneId.");
				return;
			}

			var temp = LightingLoadChangeRequest;
			temp?.Invoke(this, new GenericTrippleEventArgs<string, string, int>(
				controlId,
				zoneId,
				load));
		}
		catch (Exception ex)
		{
			SendError($"Invalid POST zone request: {ex.Message}");
		}
	}

	private void PostSceneHandler(ResponseBase response)
	{
		try
		{
			if (response.Data is not JObject data)
			{
				SendError($"Invalid zone POST requests - Invalid data type. { response.Data.GetType() }");
				return;
			}
				
			var controlId = data.Value<string>("Id") ?? string.Empty;
			var sceneId = data.Value<string>("SceneId") ?? string.Empty;

			if (string.IsNullOrEmpty(controlId) || string.IsNullOrEmpty(sceneId))
			{
				SendError($"Invalid POST scene request: Missing controlId or sceneId.");
				return;
			}

			var temp = LightingSceneRecallRequest;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(controlId, sceneId));
		}
		catch (Exception ex)
		{
			SendError($"Invalid POST scene request: {ex.Message}");
		}
	}

	private void SendError(string message)
	{
		var errRx = MessageFactory.CreateErrorResponse(message);
		Send(errRx, ApiHooks.LightingControl);
	}
}