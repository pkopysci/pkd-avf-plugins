namespace CrComLibUi.Components.AudioControl;

using Api;
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
	private const string CommandConfig = "CONFIG";
	private const string CommandRoute = "ROUTE";
	private const string CommandMute = "MUTE";
	private const string CommandLevel = "LEVEL";
	private const string CommandMicZone = "ZONE";
	private readonly Dsp _audioDsp;
	private List<AudioChannel> _inputs;
	private List<AudioChannel> _outputs;

	public AudioControlComponent(BasicTriListWithSmartObject ui, UserInterfaceDataContainer uiData)
		: base(ui, uiData)
	{
		GetHandlers.Add(CommandConfig, HandleGetConfigRequest);
		GetHandlers.Add(CommandRoute, HandleGetRouteRequest);
		GetHandlers.Add(CommandLevel, HandleGetLevelRequest);
		GetHandlers.Add(CommandMute, HandleGetMuteRequest);
		GetHandlers.Add(CommandMicZone, HandleGetMicZoneRequest);

		PostHandlers.Add(CommandLevel, HandlePostLevelRequest);
		PostHandlers.Add(CommandMute, HandlePostMuteRequest);
		PostHandlers.Add(CommandMicZone, HandlePostMicZoneRequest);
		PostHandlers.Add(CommandRoute, HandlePostRouteRequest);

		_inputs = [];
		_outputs = [];

		// TODO: add support for audio DSP information once the framework exposes that API
		_audioDsp = new Dsp() { Id = "Placeholder", IsOnline = true, Manufacturer = "FAKE", Model = "Some DSP Model" };
	}

	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? AudioOutputLevelUpRequest;
	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? AudioOutputLevelDownRequest;
	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? AudioOutputMuteChangeRequest;
	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? AudioInputLevelUpRequest;
	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? AudioInputLevelDownRequest;
	/// <inheritdoc/>
	public event EventHandler<GenericSingleEventArgs<string>>? AudioInputMuteChangeRequest;
	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputRouteRequest;
	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, string>>? AudioZoneEnableToggleRequest;
	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, int>>? SetAudioInputLevelRequest;
	/// <inheritdoc/>
	public event EventHandler<GenericDualEventArgs<string, int>>? SetAudioOutputLevelRequest;

	#region Public Methods
	/// <inheritdoc/>
	public override void HandleSerialResponse(string response)
	{
		if (!CheckInitialized("AudioControlComponent", "HandleSerialResponse")) return;
		var rxObj = MessageFactory.DeserializeMessage(response);
		var method = rxObj.Method.ToUpper();
		switch (method)
		{
			case "GET":
				HandleGetRequest(rxObj);
				break;
			case "POST":
				HandlePostRequest(rxObj);
				break;
			default:
				Send(MessageFactory.CreateErrorResponse(
						$"HTTP Method {method} not supported."),
					ApiHooks.AudioControl);
				break;
		}
	}

	/// <inheritdoc/>
	public override void Initialize()
	{
		if (_inputs.Count == 0 || _outputs.Count == 0)
			Logger.Debug("CrComLibUi.AudioControlComponent.Initialize() - no audio inputs and/or outputs set.");
		Initialized = true;
	}

	/// <inheritdoc/>
	public void SetAudioData(
		ReadOnlyCollection<AudioChannelInfoContainer> inputs,
		ReadOnlyCollection<AudioChannelInfoContainer> outputs)
	{
		_inputs.Clear();
		_outputs.Clear();
		_inputs = CreateChannelCollection(inputs);
		_outputs = CreateChannelCollection(outputs);
	}

	/// <inheritdoc/>
	public void UpdateAudioInputLevel(string id, int newLevel)
	{
		if (!CheckInitialized("AudioControlComponent", "UpdateAudioInputLevel")) return;
		if (string.IsNullOrEmpty(id))
		{
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioInputLevel() - argument 'id' cannot be null or empty.");
			return;
		}

		var found = _inputs.FirstOrDefault(x => x.Id == id);
		if (found == null)
		{
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioInputLevel() - no input found with id {0}", id);
			return;
		}

		found.Level = newLevel;
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandLevel;
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
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioInputMute() - argument 'id' cannot be null or empty.");
			return;
		}

		var found = _inputs.FirstOrDefault(x => x.Id == id);
		if (found == null)
		{
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioInputMute() - no input found with id {0}", id);
			return;
		}

		found.MuteState = muteState;
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandMute;
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
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioOutputLevel() - argument 'id' cannot be null or empty.");
			return;
		}

		var found = _outputs.FirstOrDefault(x => x.Id == id);
		if (found == null)
		{
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioOutputLevel() - no output found with id {0}", id);
			return;
		}

		found.Level = newLevel;
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandLevel;
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
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioOutputMute() - argument 'id' cannot be null or empty.");
			return;
		}

		var found = _outputs.FirstOrDefault(x => x.Id == id);
		if (found == null)
		{
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioOutputMute() - no output found with id {0}", id);
			return;
		}

		found.MuteState = muteState;
		ResponseBase message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandMute;
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
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioOutputRoute() - argument 'srcId' cannot be null or empty.");
			return;
		}

		if (string.IsNullOrEmpty(destId))
		{
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioOutputRoute() - argument 'destId' cannot be null or empty.");
			return;
		}

		var found = _outputs.FirstOrDefault(x => x.Id == destId);
		if (found == null)
		{
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioOutputRoute() - no output found with id {0}", destId);
			return;
		}

		found.RoutedInput = srcId;
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandRoute;
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
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioZoneState() - argument 'channelId' cannot be null or empty.");
			return;
		}

		if (string.IsNullOrEmpty(zoneId))
		{
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioZoneState() - argument 'zoneId' cannot be null or empty.");
			return;
		}

		var channel = _inputs.FirstOrDefault(x => x.Id.Equals(channelId, StringComparison.OrdinalIgnoreCase));
		if (channel == null)
		{
			Logger.Error("CrComLibUi.AudioControlComponent.UpdateAudioZoneState() - no input found with id {0}", channelId);
			return;
		}

		foreach (var zone in channel.Zones)
		{
			if (!zone.Id.Equals(zoneId)) continue;
			zone.Enabled = newState;
			break;
		}

		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandMicZone;
		message.Data.InId = channel.Id;
		message.Data.Zones = channel.Zones;
		Send(message, ApiHooks.AudioControl);
	}
	#endregion

	#region Private Methods
	private void HandleGetConfigRequest(ResponseBase response)
	{
		var config = new AudioConfigData()
		{
			Dsp = _audioDsp,
			Inputs = _inputs,
			Outputs = _outputs,
		};

		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandConfig;
		message.Data = config;
		Send(message, ApiHooks.AudioControl);
	}

	private void HandleGetRouteRequest(ResponseBase response)
	{
		try
		{
			var found = _outputs.FirstOrDefault(x => x.Id.Equals(response.Data.OutId));
			if (found == null)
			{
				var errRx = MessageFactory.CreateErrorResponse(
					$"No audio output found with id {response.Data.OutId}");
				Send(errRx, ApiHooks.AudioControl);
				return;
			}

			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CommandRoute;
			message.Data.OutId = found.Id;
			message.Data.InId = found.RoutedInput;
			Send(message, ApiHooks.AudioControl);
		}
		catch (Exception e)
		{
			var errRx = MessageFactory.CreateErrorResponse($"Invalid Route Query: {e.Message}");
			Send(errRx, ApiHooks.AudioControl);
		}
	}

	private void HandleGetLevelRequest(ResponseBase response)
	{
		try
		{
			AudioChannel? found = null;
			if (response.Data.Io.Equals("OUTPUT"))
			{
				found = _outputs.FirstOrDefault(x => x.Id.Equals(response.Data.Id));
			}
			else if (response.Data.Io.Equals("INPUT"))
			{
				found = _inputs.FirstOrDefault(x => x.Id.Equals(response.Data.Id));
			}
			else
			{
				var errRx = MessageFactory.CreateErrorResponse($"Unsupported Io type: {response.Data.Io}");
				Send(errRx, ApiHooks.AudioControl);
			}

			if (found == null)
			{
				var errRx = MessageFactory.CreateErrorResponse(
					$"No audio output or input found with id {response.Data.OutId}");
				Send(errRx, ApiHooks.AudioControl);
				return;
			}

			response.Data.Level = found.Level;
			Send(response, ApiHooks.AudioControl);
		}
		catch (Exception e)
		{
			var errRx = MessageFactory.CreateErrorResponse($"Invalid Level Query: {e.Message}");
			Send(errRx, ApiHooks.AudioControl);
		}
	}

	private void HandleGetMuteRequest(ResponseBase response)
	{
		try
		{
			AudioChannel? found = null;
			if (response.Data.Io.Equals("OUTPUT"))
			{
				found = _outputs.FirstOrDefault(x => x.Id.Equals(response.Data.Id));
			}
			else if (response.Data.Io.Equals("INPUT"))
			{
				found = _inputs.FirstOrDefault(x => x.Id.Equals(response.Data.Id));
			}
			else
			{
				var errRx = MessageFactory.CreateErrorResponse($"Unsupported Io type: {response.Data.Io}");
				Send(errRx, ApiHooks.AudioControl);
			}

			if (found == null)
			{
				var errRx = MessageFactory.CreateErrorResponse(
					$"No audio output or input found with id {response.Data.OutId}");
				Send(errRx, ApiHooks.AudioControl);
				return;
			}

			response.Data.Mute = found.MuteState;
			Send(response, ApiHooks.AudioControl);
		}
		catch (Exception e)
		{
			var errRx = MessageFactory.CreateErrorResponse($"Invalid Mute Query: {e.Message}");
			Send(errRx, ApiHooks.AudioControl);
		}
	}

	private void HandleGetMicZoneRequest(ResponseBase response)
	{
		try
		{
			var found = _inputs.FirstOrDefault(x => x.Id.Equals(response.Data.InId));
			if (found == null)
			{
				var errRx = MessageFactory.CreateErrorResponse(
					$"No audio input found with id {response.Data.OutId}");
				Send(errRx, ApiHooks.AudioControl);
				return;
			}

			response.Data.Zones = found.Zones;
			Send(response, ApiHooks.AudioControl);

		}
		catch (Exception e)
		{
			var errRx = MessageFactory.CreateErrorResponse($"Invalid Zone Query: {e.Message}");
			Send(errRx, ApiHooks.AudioControl);
		}
	}

	private void HandlePostLevelRequest(ResponseBase response)
	{
		try
		{
			if (response.Data is not JObject data)
			{
				var errRx = MessageFactory.CreateErrorResponse($"Invalid Data Type: {response.Data.GetType()}");
				Send(errRx, ApiHooks.AudioControl);
				return;
			}
			
			var level = data.Value<int>("Level");
			var id = data.Value<string>("Id") ?? string.Empty;
			var io = data.Value<string>("Io") ?? string.Empty;

			switch (io)
			{
				case "OUTPUT":
				{
					var temp = SetAudioOutputLevelRequest;
					temp?.Invoke(this, new GenericDualEventArgs<string, int>(id, level));
					break;
				}
				case "INPUT":
				{
					var temp = SetAudioInputLevelRequest;
					temp?.Invoke(this, new GenericDualEventArgs<string, int>(id, level));
					break;
				}
				default:
				{
					var errRx = MessageFactory.CreateErrorResponse($"Unsupported Io type: {io}");
					Send(errRx, ApiHooks.AudioControl);
					break;
				}
			}
		}
		catch (Exception e)
		{
			var errRx = MessageFactory.CreateErrorResponse($"Invalid level Post formatting: {e.Message}");
			Send(errRx, ApiHooks.AudioControl);
		}
	}

	private void HandlePostMuteRequest(ResponseBase response)
	{
		try
		{
			if (response.Data is not JObject data)
			{
				var errRx = MessageFactory.CreateErrorResponse($"Invalid Data Type: {response.Data.GetType()}");
				Send(errRx, ApiHooks.AudioControl);
				return;
			}
			
			var id = data.Value<string>("Id") ?? string.Empty;
			var io = data.Value<string>("Io") ?? string.Empty;
			switch (io)
			{
				case "OUTPUT":
				{
					var temp = AudioOutputMuteChangeRequest;
					temp?.Invoke(this, new GenericSingleEventArgs<string>(id));
					break;
				}
				case "INPUT":
				{
					var temp = AudioInputMuteChangeRequest;
					temp?.Invoke(this, new GenericSingleEventArgs<string>(id));
					break;
				}
				default:
				{
					var errRx = MessageFactory.CreateErrorResponse($"Unsupported Io type: {io}");
					Send(errRx, ApiHooks.AudioControl);
					break;
				}
			}
		}
		catch (Exception e)
		{
			var errRx = MessageFactory.CreateErrorResponse($"Invalid mute Post formatting: {e.Message}");
			Send(errRx, ApiHooks.AudioControl);
		}
	}

	private void HandlePostRouteRequest(ResponseBase response)
	{
		try
		{
			if (response.Data is not JObject data)
			{
				var errRx = MessageFactory.CreateErrorResponse($"Invalid Data Type: {response.Data.GetType()}");
				Send(errRx, ApiHooks.AudioControl);
				return;
			}
			
			var outId = data.Value<string>("OutId") ?? string.Empty;
			var inId = data.Value<string>("InId") ?? string.Empty;
			if (outId.Equals(string.Empty) || inId.Equals(string.Empty))
			{
				var errRx = MessageFactory.CreateErrorResponse($"Invalid route post formatting. Missing OutId or InId.");
				Send(errRx, ApiHooks.AudioControl);
				return;
			}
			
			var temp = AudioOutputRouteRequest;
			temp?.Invoke(
				this,
				new GenericDualEventArgs<string, string>(inId, outId)
			);
		}
		catch (Exception e)
		{
			Logger.Error($"CrComLobUi.AudioControlComponent.HandlePostRouteRequest(): {e.Message}");
			var errRx = MessageFactory.CreateErrorResponse("ERR 500 - Internal Server Error.");
			Send(errRx, ApiHooks.AudioControl);
		}
	}

	private void HandlePostMicZoneRequest(ResponseBase response)
	{
		try
		{
			if (response.Data is not JObject data)
			{
				var errRx = MessageFactory.CreateErrorResponse($"Invalid Data Type: {response.Data.GetType()}");
				Send(errRx, ApiHooks.AudioControl);
				return;
			}
			
			var inId = data.Value<string>("InId");
			var zoneId = data.Value<string>("ZoneId");
			if (string.IsNullOrEmpty(inId) || string.IsNullOrEmpty(zoneId))
			{
				var errRx = MessageFactory.CreateErrorResponse($"Invalid zone change post formatting. Missing InId or ZoneId. ");
				Send(errRx, ApiHooks.AudioControl);
				return;
			}
			
			var temp = AudioZoneEnableToggleRequest;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(inId, zoneId));
		}
		catch (Exception e)
		{
			var errRx = MessageFactory.CreateErrorResponse($"Invalid zone change post formatting: {e.Message}");
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
			var errRx = MessageFactory.CreateErrorResponse($"Unsupported GET command: {response.Command}");
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
			var errRx = MessageFactory.CreateErrorResponse($"Unsupported POST command: {response.Command}");
			Send(errRx, ApiHooks.AudioControl);
		}
	}

	private List<AudioChannel> CreateChannelCollection(ReadOnlyCollection<AudioChannelInfoContainer> data)
	{
		List<AudioChannel> audioChannels = [];
		foreach (var channel in data)
		{
			List<EnableZone> zones = [];
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