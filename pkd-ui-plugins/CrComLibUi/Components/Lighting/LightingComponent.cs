﻿namespace CrComLibUi.Components.Lighting
{
	using Crestron.SimplSharpPro.DeviceSupport;
	using pkd_application_service.LightingControl;
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
    using Newtonsoft.Json.Linq;
    using Independentsoft.Exchange;
    using Crestron.SimplSharpPro.Fusion;

    internal class LightingComponent : BaseComponent, ILightingUserInterface
	{
		private static readonly string COMMAND_CONFIG = "CONFIG";
		private static readonly string COMMAND_ZONE = "LOAD";
		private static readonly string COMMAND_SCENE = "SCENE";
		private List<LightingData> lights;

		public LightingComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
			: base(ui, uiData)
		{
			GetHandlers.Add(COMMAND_CONFIG, GetConfigHandler);
			GetHandlers.Add(COMMAND_SCENE, GetSceneHandler);
			GetHandlers.Add(COMMAND_ZONE, GetZoneHandler);
			PostHandlers.Add(COMMAND_ZONE, PostZoneHandler);
			PostHandlers.Add(COMMAND_SCENE, PostSceneHandler);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> LightingSceneRecallRequest;
		/// <inheritdoc/>
		public event EventHandler<GenericTrippleEventArgs<string, string, int>> LightingLoadChangeRequest;

		/// <inheritdoc/>
		public override void HandleSerialResponse(string response)
		{
			try
			{
				ResponseBase message = MessageFactory.DeserializeMessage(response);
                if (message == null)
                {
					ResponseBase errMsg = MessageFactory.CreateErrorResponse("Invalid message format.");
					Send(errMsg, ApiHooks.LightingControl);
					return;
                }

				if (message.Method.Equals("GET"))
				{
					HandleGetRequests(message);
				}
				else if (message.Method.Equals("POST"))
				{
					HandlePostRequests(message);
				}
				else
				{
					ResponseBase errMessage = MessageFactory.CreateErrorResponse($"Unsupported method: {message.Method}.");
					Send(errMessage, ApiHooks.LightingControl);
					return;
				}
			}
			catch (Exception ex)
			{
				Logger.Error("CrComLibUI.LightingComponent.HandleSerialResponse() - {0}", ex);
				ResponseBase errMessage = MessageFactory.CreateErrorResponse("Invalid message format.");
				Send(errMessage, ApiHooks.LightingControl);
				return;
			}
		}

		/// <inheritdoc/>
		public override void Initialize()
		{
			Initialized = false;
			if (lights == null)
			{
				Logger.Error("CrComLibUI.LightingComponent.Initialize() - Call SetLightingData() first.");
				return;
			}

			Logger.Debug("CrComLibUI.LightingComponent.Initialize()");

			Initialized = true;
		}

		/// <inheritdoc/>
		public void SetLightingData(ReadOnlyCollection<LightingControlInfoContainer> lightingData)
		{
			if (lightingData == null)
			{
				Logger.Error("CrComLibUI.LightingComponent.SetLightingData() - argument 'lightingData' cannot be null.");
				return;
			}

			lights = new List<LightingData>();
			foreach (var controller in lightingData)
			{
				List<LightingSceneData> scenes = new List<LightingSceneData>();
				foreach (var scene in controller.Scenes)
				{
					scenes.Add(new LightingSceneData()
					{
						Id = scene.Id,
						Label = scene.Label,
						Tags = scene.Tags,
					});
				}

				List<LightingZoneData> zones = new List<LightingZoneData>();
				foreach (var zone in controller.Zones)
				{
					zones.Add(new LightingZoneData()
					{
						Id = zone.Id,
						Label = zone.Label,
						Tags = zone.Tags,
					});
				}

				LightingData data = new LightingData()
				{
					Id = controller.Id,
					Label = controller.Label,
					Tags = controller.Tags,
					Scenes = scenes,
					Zones = zones
				};

				lights.Add(data);
			}
		}

		/// <inheritdoc/>
		public void UpdateActiveLightingScene(string controlId, string sceneId)
		{
			var control = lights.FirstOrDefault(x => x.Id == controlId);
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
			message.Command = COMMAND_SCENE;
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
			var control = lights.FirstOrDefault(x => x.Id == controlId);
			if (control == null)
			{
				Logger.Error("CrComLibUI.LightingComponent.UpdateActiveLightingScene() - No controller with ID {0}", controlId);
				return;
			}

			foreach (var zone in control.Zones)
			{
				if (zone.Id.Equals(zoneId))
				{
					zone.Load = level;
					var message = MessageFactory.CreateGetResponseObject();
					message.Command = COMMAND_ZONE;
					message.Data = new ExpandoObject();
					message.Data.Id = controlId;
					message.Data.ZoneId = zoneId;
					message.Data.Load = true;
					Send(message, ApiHooks.LightingControl);
					break;
				}
			}
		}

		private LightingData FindController(string id, string command)
		{
			var controller = lights.FirstOrDefault(x => x.Id.Equals(id));
			if (controller == null)
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"No light control with ID {id}.");
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
			response.Data = lights;
			Send(response, ApiHooks.LightingControl);
		}

		private void GetZoneHandler(ResponseBase response)
		{
			try
			{
				JObject data = response.Data as JObject;
				string controlId = data?.Value<string>("Id") ?? string.Empty;
				string zoneId = data?.Value<string>("ZoneId") ?? string.Empty;

                LightingData controller = FindController(controlId, COMMAND_ZONE);
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
				JObject data = response.Data as JObject;
				string controlId = data?.Value<string>("Id") ?? string.Empty;
				string sceneId = data?.Value<string>("SceneId") ?? string.Empty;

                LightingData controller = FindController(controlId, COMMAND_SCENE);
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
				JObject data = response.Data as JObject;
				string controlId = data?.Value<string>("Id");
				string zoneId = data?.Value<string>("ZoneId");
				int load = data?.Value<int>("Load") ?? -1;

				if (controlId == null || zoneId == null || load < 0)
				{
					SendError("data missing Id, ZoneId, or contains an invalid Load value");
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
				JObject data = response.Data as JObject;
				string controlId = data?.Value<string>("Id");
				string sceneId = data?.Value<string>("SceneId");

				if (controlId == null || sceneId == null)
				{
					SendError($"Invalid POST scene request: Id or SceneId not present.");
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
}
