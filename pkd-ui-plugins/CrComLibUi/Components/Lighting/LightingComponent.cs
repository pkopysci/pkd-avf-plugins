﻿namespace CrComLibUi.Components.Lighting;

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
			if (!scene.Set) continue;
			
			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CommandScene;
			message.Data["SceneId"] = scene.Id;
			Send(message, ApiHooks.LightingControl);
		}
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
			break;
		}
	}

	private void HandleGetRequests(ResponseBase response)
	{
		if (GetHandlers.TryGetValue(response.Command, out var handler))
		{
			handler.Invoke(response);
		}
		else
		{
			SendError($"Unsupported GET command: {response.Command}", ApiHooks.LightingControl);
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
			SendError($"Unsupported POST command: {response.Command}", ApiHooks.LightingControl);
		}
	}

	private void GetConfigHandler(ResponseBase response)
	{
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandConfig;
		message.Data["Lights"] = JToken.FromObject(_lights);
		Send(message, ApiHooks.LightingControl);
	}

	private void GetZoneHandler(ResponseBase response)
	{
		try
		{
			var id = response.Data.Value<string>("Id");
			var zoneId = response.Data.Value<string>("ZoneId");
			if (string.IsNullOrEmpty(zoneId) || string.IsNullOrEmpty(id))
			{
				SendError("Invalid GET zone request - missing Id or ZoneId.", ApiHooks.LightingControl);
				return;
			}
			
			var light = _lights.FirstOrDefault(x => x.Id == id);
			if (light == null)
			{
				SendError($"Invalid Get zone request - no lighting control with id {id}", ApiHooks.LightingControl);
				return;
			}
			
			var zone = light.Zones.FirstOrDefault(x => x.Id == zoneId);
			if (zone == null)
			{
				SendError($"Invalid GET zone request - control {id} does not have a zone with id {zoneId}", ApiHooks.LightingControl);
				return;
			}
			
			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CommandZone;
			message.Data["ZoneId"] = zoneId;
			message.Data["Id"] = id;
			Send(message, ApiHooks.LightingControl);
		}
		catch (Exception e)
		{
			Logger.Error("CrComLibUI.LightingComponent.GetZoneHandler() - {0}", e.Message);
			SendServerError(ApiHooks.LightingControl);
		}
	}

	private void GetSceneHandler(ResponseBase response)
	{
		try
		{
			var controlId = response.Data.Value<string>("Id");
			if (string.IsNullOrEmpty(controlId))
			{
				SendError("Invalid GET scene handler - missing Id.", ApiHooks.LightingControl);
				return;
			}
			
			var found = _lights.FirstOrDefault(x => x.Id == controlId);
			if (found == null)
			{
				SendError($"Invalid GET scene handler, no control with id {controlId}", ApiHooks.LightingControl);
				return;
			}
			
			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CommandScene;
			message.Data["SceneId"] = found.Scenes.First(x => x.Set).Id;
			Send(message, ApiHooks.LightingControl);
		}
		catch (Exception e)
		{
			Logger.Error("CrComLibUI.LightingComponent.GetSceneHandler() - {0}", e.Message);
			SendServerError(ApiHooks.LightingControl);
		}
	}

	private void PostZoneHandler(ResponseBase response)
	{
		try
		{
			var controlId = response.Data.Value<string>("Id");
			var zoneId = response.Data.Value<string>("ZoneId");
			var load = response.Data.Value<int>("Load");
			if (string.IsNullOrEmpty(controlId) || string.IsNullOrEmpty(zoneId) || load < 0)
			{
				SendError(
					"Invalid POST zone request - missing Id or ZoneId, or Load is less than zero.",
					ApiHooks.LightingControl);
				return;
			}
			
			var control = _lights.FirstOrDefault(x => x.Id == zoneId);
			if (control == null)
			{
				SendError(
					$"Invalid POST zone request - no control with id {controlId}",
					ApiHooks.LightingControl);
				return;
			}

			var zone = control.Zones.FirstOrDefault(x => x.Id == zoneId);
			if (zone == null)
			{
				SendError(
					$"Invalid POST zone request - control {controlId} does not have a zone with id {zoneId}",
					ApiHooks.LightingControl);
				return;
			}

			var temp = LightingLoadChangeRequest;
			temp?.Invoke(this, new GenericTrippleEventArgs<string, string, int>(control.Id, zoneId, load));
		}
		catch (Exception e)
		{
			Logger.Error("CrComLibUI.LightingComponent.PostZoneHandler() - {0}", e.Message);
			SendServerError(ApiHooks.LightingControl);
		}
	}

	private void PostSceneHandler(ResponseBase response)
	{
		try
		{
			var controlId = response.Data.Value<string>("Id");
			var sceneId = response.Data.Value<string>("SceneId");
			if (string.IsNullOrEmpty(controlId) || string.IsNullOrEmpty(sceneId))
			{
				SendError("Invalid POST scene request - missing Id or SceneId.", ApiHooks.LightingControl);
				return;
			}
			
			var control = _lights.FirstOrDefault(x => x.Id == controlId);
			if (control == null)
			{
				SendError(
					$"Invalid POST scene request - no control with id {controlId}",
					ApiHooks.LightingControl);
				return;
			}

			var temp = LightingSceneRecallRequest;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(control.Id, sceneId));
		}
		catch (Exception e)
		{
			Logger.Error("CrComLibUI.LightingComponent.PostSceneHandler() - {0}", e.Message);
			SendServerError(ApiHooks.LightingControl);
		}
	}
}