namespace CrComLibUi.Components.AudioControl
{
	using CrComLibUi.Api;
	using Crestron.SimplSharpPro.DeviceSupport;
    using Newtonsoft.Json.Linq;
    using pkd_application_service.AudioControl;
	using pkd_application_service.UserInterface;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_ui_service.Interfaces;
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;

	internal class AudioControlComponent : BaseComponent, IAudioUserInterface, IAudioDiscreteLevelUserInterface
	{
		private static readonly string COMMAND_CONFIG = "CONFIG";
		private static readonly string COMMAND_STATUS = "CONNECTIONSTATUS";
		private static readonly string COMMAND_ROUTE = "ROUTE";
		private static readonly string COMMAND_MUTE = "MUTE";
		private static readonly string COMMAND_LEVEL = "LEVEL";
		private static readonly string COMMAND_LEVEL_ADJUST = "LEVELADJUST";
		private static readonly string COMMAND_MIC_ZONE = "ZONE";
		private readonly Dsp audioDsp;
		private List<AudioChannel> inputs;
		private List<AudioChannel> outputs;

		public AudioControlComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
			: base(ui, uiData)
		{
			GetHandlers.Add(COMMAND_CONFIG, HandleGetConfigRequest);
			GetHandlers.Add(COMMAND_ROUTE, HandleGetRouteRequest);
			GetHandlers.Add(COMMAND_LEVEL, HandleGetLevelRequest);
			GetHandlers.Add(COMMAND_MUTE, HandleGetMuteRequest);
			GetHandlers.Add(COMMAND_MIC_ZONE, HandleGetMicZoneRequest);

			PostHandlers.Add(COMMAND_LEVEL, HandlePostLevelRequest);
			PostHandlers.Add(COMMAND_MUTE, HandlePostMuteRequest);
			PostHandlers.Add(COMMAND_MIC_ZONE, HandlePostMicZoneRequest);
			PostHandlers.Add(COMMAND_ROUTE, HandlePostRouteRequest);

			this.inputs = new List<AudioChannel>();
			this.outputs = new List<AudioChannel>();

			// TODO: add support for audio DSP information once the framework exposes that API
			this.audioDsp = new Dsp() { Id = "Placeholder", IsOnline = true, Manufacturer = "FAKE", Model = "Some DSP Model" };
		}

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> AudioOutputLevelUpRequest;
		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> AudioOutputLevelDownRequest;
		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> AudioOutputMuteChangeRequest;
		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> AudioInputLevelUpRequest;
		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> AudioInputLevelDownRequest;
		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> AudioInputMuteChangeRequest;
		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioOutputRouteRequest;
		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioZoneEnableToggleRequest;
		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, int>> SetAudioInputLevelRequest;
		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, int>> SetAudioOutputLevelRequest;

		#region Public Methods
		/// <inheritdoc/>
		public override void HandleSerialResponse(string response)
		{
			if (!CheckInitialized("AudioControlComponent", "HandleSerialResponse")) return;
			
			ResponseBase rxObj = MessageFactory.DeserializeMessage(response);
			if (rxObj == null)
			{
				Send(MessageFactory.CreateErrorResponse("Invalid Message Format."), ApiHooks.AudioControl);
				return;
			}

			string method = rxObj.Method.ToUpper();
			if (method.Equals("GET"))
			{
				HandleGetRequest(rxObj);
			}
			else if (method.Equals("POST"))
			{
				HandlePostRequest(rxObj);
			}
			else
			{
				Send(MessageFactory.CreateErrorResponse(
					$"HTTP Method {method} not supported."),
					ApiHooks.AudioControl);
			}
		}

		/// <inheritdoc/>
		public override void Initialize()
		{
			Initialized = false;
			if (uiData == null)
			{
				Logger.Error("GcuVueUi.AudioControlComponent.Initialize() - Set UiData first.");
				return;
			}

			if (inputs == null || outputs == null)
			{
				Logger.Error("GcuVueUi.AudioControlComponent.Initialize() - set inputs and outputs data first (call SetAudioData()).");
				return;
			}

			Initialized = true;
		}

		/// <inheritdoc/>
		public void SetAudioData(
			ReadOnlyCollection<AudioChannelInfoContainer> inputs,
			ReadOnlyCollection<AudioChannelInfoContainer> outputs)
		{
			if (inputs == null)
			{
				Logger.Error("GcuVueUi.AudioControlComponent.SetAudioData() - argument 'inputs' cannot be null.");
				return;
			}
			if (outputs == null)
			{
				Logger.Error("GcuVueUi.AudioControlComponent.SetAudioData() - argument 'outputs' cannot be null.");
				return;
			}

			this.inputs.Clear();
			this.outputs.Clear();
			this.inputs = CreateChannelCollection(inputs);
			this.outputs = CreateChannelCollection(outputs);
		}


		/// <inheritdoc/>
		public void UpdateAudioInputLevel(string id, int newLevel)
		{
			if (!CheckInitialized("AudioControlComponent", "UpdateAudioInputLevel")) return;
			if (string.IsNullOrEmpty(id))
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioInputLevel() - argument 'id' cannot be null or empty.");
				return;
			}

			var found = inputs.FirstOrDefault(x => x.Id == id);
			if (found == null)
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioInputLevel() - no input found with id {0}", id);
				return;
			}

			found.Level = newLevel;
			ResponseBase message = MessageFactory.CreateGetResponseObject();
			message.Command = COMMAND_LEVEL;
			message.Data.Io = "INPUT";
			message.Data.Id = id;
			message.Data.Level = newLevel;
			Send(message, ApiHooks.AudioControl);
		}

		/// <inheritdoc/>
		public void UpdateAudioInputMute(string id, bool muteState)
		{
			if (!CheckInitialized("AudioControlComponent", "UpdateAudioInputMute")) return;
			if (string.IsNullOrEmpty(id))
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioInputMute() - argument 'id' cannot be null or empty.");
				return;
			}

			var found = inputs.FirstOrDefault(x => x.Id == id);
			if (found == null)
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioInputMute() - no input found with id {0}", id);
				return;
			}

			found.MuteState = muteState;
			ResponseBase message = MessageFactory.CreateGetResponseObject();
			message.Command = COMMAND_MUTE;
			message.Data.Io = "INPUT";
			message.Data.Id = id;
			message.Data.Mute = muteState;
			Send(message, ApiHooks.AudioControl);
		}

		/// <inheritdoc/>
		public void UpdateAudioOutputLevel(string id, int newLevel)
		{
			if (!CheckInitialized("AudioControlComponent", "UpdateAudioOutputLevel")) return;
			if (string.IsNullOrEmpty(id))
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioOutputLevel() - argument 'id' cannot be null or empty.");
				return;
			}

			var found = outputs.FirstOrDefault(x => x.Id == id);
			if (found == null)
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioOutputLevel() - no output found with id {0}", id);
				return;
			}

			found.Level = newLevel;
			ResponseBase message = MessageFactory.CreateGetResponseObject();
			message.Command = COMMAND_LEVEL;
			message.Data.Io = "OUTPUT";
			message.Data.Id = id;
			message.Data.Level = newLevel;
			Send(message, ApiHooks.AudioControl);
		}

		/// <inheritdoc/>
		public void UpdateAudioOutputMute(string id, bool muteState)
		{
			if (!CheckInitialized("AudioControlComponent", "UpdateAudioOutputMute")) return;
			if (string.IsNullOrEmpty(id))
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioOutputMute() - argument 'id' cannot be null or empty.");
				return;
			}

			var found = outputs.FirstOrDefault(x => x.Id == id);
			if (found == null)
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioOutputMute() - no output found with id {0}", id);
				return;
			}

			found.MuteState = muteState;
			ResponseBase message = MessageFactory.CreateGetResponseObject();
			message.Command = COMMAND_MUTE;
			message.Data.Io = "OUTPUT";
			message.Data.Id = id;
			message.Data.Mute = muteState;
			Send(message, ApiHooks.AudioControl);
		}

		/// <inheritdoc/>
		public void UpdateAudioOutputRoute(string srcId, string destId)
		{
			if (!CheckInitialized("AudioControlComponent", "UpdateAudioOutputRoute")) return;

			if (string.IsNullOrEmpty(srcId))
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioOutputRoute() - argument 'srcId' cannot be null or empty.");
				return;
			}

			if (string.IsNullOrEmpty(destId))
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioOutputRoute() - argument 'destId' cannot be null or empty.");
				return;
			}

			var found = outputs.FirstOrDefault(x => x.Id == destId);
			if (found == null)
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioOutputRoute() - no output found with id {0}", destId);
				return;
			}

			found.RoutedInput = srcId;
			ResponseBase message = MessageFactory.CreateGetResponseObject();
			message.Command = COMMAND_ROUTE;
			message.Data.OutId = destId;
			message.Data.InId = srcId;
			Send(message, ApiHooks.AudioControl);
		}

		/// <inheritdoc/>
		public void UpdateAudioZoneState(string channelId, string zoneId, bool newState)
		{
			if (!CheckInitialized("AudioControlComponent", "UpdateAudioZoneState")) return;

			if (string.IsNullOrEmpty(channelId))
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioZoneState() - argument 'channelId' cannot be null or empty.");
				return;
			}

			if (string.IsNullOrEmpty(zoneId))
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioZoneState() - argument 'zoneId' cannot be null or empty.");
				return;
			}

			var channel = inputs.FirstOrDefault(x => x.Id.Equals(channelId, StringComparison.OrdinalIgnoreCase));
			if (channel == null)
			{
				Logger.Error("GcuVueUi.AudioControlComponent.UpdateAudioZoneState() - no input found with id {0}", channelId);
				return;
			}

			foreach (var zone in channel.Zones)
			{
				if (zone.Id.Equals(zoneId))
				{
					zone.Enabled = newState;
					break;
				}
			}

			ResponseBase message = MessageFactory.CreateGetResponseObject();
			message.Command = COMMAND_MIC_ZONE;
			message.Data.InId = channel.Id;
			message.Data.Zones = channel.Zones;
			Send(message, ApiHooks.AudioControl);
		}
		#endregion

		#region Private Methods
		private void HandleGetConfigRequest(ResponseBase response)
		{
			AudioConfigData config = new AudioConfigData()
			{
				Dsp = audioDsp,
				Inputs = inputs,
				Outputs = outputs,
			};

			var message = MessageFactory.CreateGetResponseObject();
			message.Command = COMMAND_CONFIG;
			message.Data = config;
			Send(message, ApiHooks.AudioControl);
		}

		private void HandleGetRouteRequest(ResponseBase response)
		{
			try
			{
				var found = outputs.FirstOrDefault(x => x.Id.Equals(response.Data.OutId));
				if (found == null)
				{
					ResponseBase errRx = MessageFactory.CreateErrorResponse(
						$"No audio output found with id {response.Data.OutId}");
					return;
				}

				ResponseBase message = MessageFactory.CreateGetResponseObject();
				message.Command = COMMAND_ROUTE;
				message.Data.OutId = found.Id;
				message.Data.InId = found.RoutedInput;
				Send(message, ApiHooks.AudioControl);
			}
			catch (Exception e)
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Invalid Route Query: {e.Message}");
				Send(errRx, ApiHooks.AudioControl);
			}
		}

		private void HandleGetLevelRequest(ResponseBase response)
		{
			try
			{
				AudioChannel found = null;
				if (response.Data.Io.Equals("OUTPUT"))
				{
					found = outputs.FirstOrDefault(x => x.Id.Equals(response.Data.Id));
				}
				else if (response.Data.Io.Equals("INPUT"))
				{
					found = inputs.FirstOrDefault(x => x.Id.Equals(response.Data.Id));
				}
				else
				{
					ResponseBase errRx = MessageFactory.CreateErrorResponse($"Unsupported Io type: {response.Data.Io}");
					Send(errRx, ApiHooks.AudioControl);
				}

				if (found == null)
				{
					ResponseBase errRx = MessageFactory.CreateErrorResponse(
						$"No audio output or input found with id {response.Data.OutId}");
					return;
				}

				response.Data.Level = found.Level;
				Send(response, ApiHooks.AudioControl);
			}
			catch (Exception e)
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Invalid Level Query: {e.Message}");
				Send(errRx, ApiHooks.AudioControl);
			}
		}

		private void HandleGetMuteRequest(ResponseBase response)
		{
			try
			{
				AudioChannel found = null;
				if (response.Data.Io.Equals("OUTPUT"))
				{
					found = outputs.FirstOrDefault(x => x.Id.Equals(response.Data.Id));
				}
				else if (response.Data.Io.Equals("INPUT"))
				{
					found = inputs.FirstOrDefault(x => x.Id.Equals(response.Data.Id));
				}
				else
				{
					ResponseBase errRx = MessageFactory.CreateErrorResponse($"Unsupported Io type: {response.Data.Io}");
					Send(errRx, ApiHooks.AudioControl);
				}

				if (found == null)
				{
					ResponseBase errRx = MessageFactory.CreateErrorResponse(
						$"No audio output or input found with id {response.Data.OutId}");
					return;
				}

				response.Data.Mute = found.MuteState;
				Send(response, ApiHooks.AudioControl);
			}
			catch (Exception e)
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Invalid Mute Query: {e.Message}");
				Send(errRx, ApiHooks.AudioControl);
			}
		}

		private void HandleGetMicZoneRequest(ResponseBase response)
		{
			try
			{
				AudioChannel found = inputs.FirstOrDefault(x => x.Id.Equals(response.Data.InId));
				if (found == null)
				{
					ResponseBase errRx = MessageFactory.CreateErrorResponse(
						$"No audio input found with id {response.Data.OutId}");
					return;
				}

				response.Data.Zones = found.Zones;
				Send(response, ApiHooks.AudioControl);

			}
			catch (Exception e)
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Invalid Zone Query: {e.Message}");
				Send(errRx, ApiHooks.AudioControl);
			}
		}

		private void HandlePostLevelRequest(ResponseBase response)
		{
			try
			{
				JObject data = response.Data as JObject;
				int level = data.Value<int>("Level"); //(int)response.Data.Level;
				string id = data.Value<string>("Id"); //response.Data.Id;
				string io = data.Value<string>("Io");

				if (io.Equals("OUTPUT"))
				{
					var temp = SetAudioOutputLevelRequest;
					temp?.Invoke(this, new GenericDualEventArgs<string, int>(id, level));
				}
				else if (io.Equals("INPUT"))
				{
					var temp = SetAudioInputLevelRequest;
					temp?.Invoke(this, new GenericDualEventArgs<string, int>(id, level));
				}
				else
				{
					ResponseBase errRx = MessageFactory.CreateErrorResponse($"Unsupported Io type: {io}");
					Send(errRx, ApiHooks.AudioControl);
				}
			}
			catch (Exception e)
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Invalid level Post formatting: {e.Message}");
				Send(errRx, ApiHooks.AudioControl);
			}
		}

		private void HandlePostMuteRequest(ResponseBase response)
		{
			try
			{
                JObject data = response.Data as JObject;
                int level = data.Value<int>("Level"); //(int)response.Data.Level;
                string id = data.Value<string>("Id"); //response.Data.Id;
                string io = data.Value<string>("Io");

                if (io.Equals("OUTPUT"))
				{
					var temp = AudioOutputMuteChangeRequest;
					temp?.Invoke(this, new GenericSingleEventArgs<string>(id));
				}
				else if (io.Equals("INPUT"))
				{
					var temp = AudioInputMuteChangeRequest;
					temp?.Invoke(this, new GenericSingleEventArgs<string>(id));
				}
				else
				{
					ResponseBase errRx = MessageFactory.CreateErrorResponse($"Unsupported Io type: {io}");
					Send(errRx, ApiHooks.AudioControl);
				}
			}
			catch (Exception e)
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Invalid mute Post formatting: {e.Message}");
				Send(errRx, ApiHooks.AudioControl);
			}
		}

		private void HandlePostRouteRequest(ResponseBase response)
		{
			try
			{
                JObject data = response.Data as JObject;
				string outId = data.Value<string>("OutId");
				string inId = data.Value<string>("InId");

                var temp = AudioOutputRouteRequest;
				temp?.Invoke(
					this,
					new GenericDualEventArgs<string, string>(outId, inId)
				);
			}
			catch (Exception e)
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Invalid route post formatting: {e.Message}");
				Send(errRx, ApiHooks.AudioControl);
			}
		}

		private void HandlePostMicZoneRequest(ResponseBase response)
		{
			try
			{
				JObject data = response.Data as JObject;
				string inId = data.Value<string>("InId");
				string zoneId = data.Value<string>("ZoneId");

				var temp = AudioZoneEnableToggleRequest;
				temp?.Invoke(this, new GenericDualEventArgs<string, string>(inId, zoneId));
			}
			catch (Exception e)
			{
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Invalid zone change post formatting: {e.Message}");
				Send(errRx, ApiHooks.AudioControl);
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
				ResponseBase errRx = MessageFactory.CreateErrorResponse($"Unsupported GET command: {response.Command}");
				Send(errRx, ApiHooks.AudioControl);
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
				Send(errRx, ApiHooks.AudioControl);
			}
		}

		private List<AudioChannel> CreateChannelCollection(ReadOnlyCollection<AudioChannelInfoContainer> data)
		{
			List<AudioChannel> audioChannels = new List<AudioChannel>();
			foreach (var channel in data)
			{
				List<EnableZone> zones = new List<EnableZone>();
				foreach (var zone in channel.ZoneEnableControls)
				{
					zones.Add(new EnableZone()
					{
						Id = zone.Id,
						Label = zone.Label,
						Enabled = zone.IsOnline,
					});
				}

				audioChannels.Add(new AudioChannel()
				{
					Icon = channel.Icon,
					Id = channel.Id,
					Label = channel.Label,
					Zones = zones,
					Tags = channel.Tags,
				});
			}

			return audioChannels;
		}
		#endregion
	}
}
