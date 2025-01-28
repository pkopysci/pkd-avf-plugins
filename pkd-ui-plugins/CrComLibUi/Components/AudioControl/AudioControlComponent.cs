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
		message.Data["Io"] = JToken.FromObject("INPUT");
		message.Data["Id"] = JToken.FromObject(id);
		message.Data["Level"] = JToken.FromObject(newLevel);
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
		message.Data["Io"] = JToken.FromObject("INPUT");
		message.Data["Id"] = JToken.FromObject(id);
		message.Data["Mute"] = JToken.FromObject(muteState);
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
		message.Data["Io"] = JToken.FromObject("OUTPUT");
		message.Data["Id"] = JToken.FromObject(id);
		message.Data["Level"] = JToken.FromObject(newLevel);
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
		
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandMute;
		message.Data["Io"] = JToken.FromObject("OUTPUT");
		message.Data["Id"] = JToken.FromObject(id);
		message.Data["Mute"] = JToken.FromObject(muteState);
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
		message.Data["OutId"] = JToken.FromObject(destId);
		message.Data["InId"] = JToken.FromObject(srcId);
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
		message.Data["InId"] = JToken.FromObject(channel.Id);
		message.Data["Zones"] = JToken.FromObject(channel.Zones);
		Send(message, ApiHooks.AudioControl);
	}
	#endregion

	#region Private Methods
	private void HandleGetConfigRequest(ResponseBase response)
	{
		var message = MessageFactory.CreateGetResponseObject();
		message.Command = CommandConfig;
		message.Data["Inputs"] = JToken.FromObject(_inputs);
		message.Data["Outputs"] = JToken.FromObject(_outputs);
		message.Data["Dsp"] = JToken.FromObject(_audioDsp);
		Send(message, ApiHooks.AudioControl);
	}

	private void HandleGetRouteRequest(ResponseBase response)
	{
		try
		{
			var destinationId = response.Data.Value<string>("OutId");
			if (string.IsNullOrEmpty(destinationId))
			{
				SendError("Invalid GET route request - missing DestId.", ApiHooks.AudioControl);
				return;
			}
			
			var dest = _outputs.FirstOrDefault(x => x.Id == destinationId);
			if (dest == null)
			{
				SendError($"Invalid Get route request - no destination found with ID {destinationId}", ApiHooks.AudioControl);
				return;
			}
			
			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CommandRoute;
			message.Data["OutId"] = JToken.FromObject(destinationId);
			message.Data["InId"] = JToken.FromObject(dest.RoutedInput);
			Send(message, ApiHooks.AudioControl);
		}
		catch (Exception ex)
		{
			Logger.Error(ex, "CrComLibUi.AudioControlComponent.HandleGetRouteRequest()");
			SendServerError(ApiHooks.AudioControl);
		}
	}

	private void HandleGetLevelRequest(ResponseBase response)
	{
		try
		{
			var io = response.Data.Value<string>("Io");
			var channelId = response.Data.Value<string>("Id");
			if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(io))
			{
				SendError("Invalid GET level request - missing Id or Io.", ApiHooks.AudioControl);
				return;
			}

			if (!TryFindChannel(io, channelId, out var channel))
			{
				SendError($"Invalid GET level request - no input or output found with id {channelId}", ApiHooks.AudioControl);
				return;
			}
			
			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CommandLevel;
			message.Data["Id"] = channelId;
			message.Data["Io"] = io;
			message.Data["Level"] = channel?.Level ?? 0;
			Send(message, ApiHooks.AudioControl);
		}
		catch (Exception ex)
		{
			Logger.Error($"CrComLibUi.AudioControlComponent.HandleGetLevelRequest() - failed to parse response: {ex.Message}");
			SendServerError(ApiHooks.AudioControl);
		}
	}

	private void HandleGetMuteRequest(ResponseBase response)
	{
		try
		{
			var io = response.Data.Value<string>("Io");
			var channelId = response.Data.Value<string>("Id");
			if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(io))
			{
				SendError("Invalid GET mute request - missing Id or Io.", ApiHooks.AudioControl);
				return;
			}

			if (!TryFindChannel(io, channelId, out var channel))
			{
				SendError($"Invalid GET mute request - no input or output found with id {channelId}", ApiHooks.AudioControl);
				return;
			}
			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CommandMute;
			message.Data["Id"] = channelId;
			message.Data["Io"] = io;
			message.Data["Mute"] = channel?.MuteState ?? false;
			Send(message, ApiHooks.AudioControl);
		}
		catch (Exception e)
		{
			Logger.Error($"CrComLibUi.HandleGetMuteRequest() - failed to parse response: {e.Message}");
			SendServerError(ApiHooks.AudioControl);
		}
	}

	private void HandleGetMicZoneRequest(ResponseBase response)
	{
		try
		{
			var id = response.Data.Value<string>("InId");
			if (string.IsNullOrEmpty(id))
			{
				SendError("Invalid GET zone request - missing InId.", ApiHooks.AudioControl);
				return;
			}
			
			var channel = _inputs.FirstOrDefault(x => x.Id.Equals(id));
			if (channel == null)
			{
				SendError($"Invalid GET zone request - no input channel with id {id}", ApiHooks.AudioControl);
				return;
			}
			
			var message = MessageFactory.CreateGetResponseObject();
			message.Command = CommandMicZone;
			message.Data["InId"] = id;
			message.Data["Zones"] = JToken.FromObject(channel.Zones);
			Send(message, ApiHooks.AudioControl);
		}
		catch (Exception e)
		{
			Logger.Error($"CrComLib.HandleGetMicZoneRequest() - failed to parse response: {e.Message}");
			SendServerError(ApiHooks.AudioControl);
		}
	}

	private void HandlePostLevelRequest(ResponseBase response)
	{
		try
		{
			var io = response.Data.Value<string>("Io");
			var channelId = response.Data.Value<string>("Id");
			var level = response.Data.Value<int>("Level");
			if (string.IsNullOrEmpty(io) || string.IsNullOrEmpty(channelId) || !response.Data.ContainsKey("Level"))
			{
				SendError("Invalid POST level request - missing Io, Id, or Level.", ApiHooks.AudioControl);
				return;
			}

			if (!TryFindChannel(io, channelId, out _))
			{
				SendError($"Invalid POST level request - no channel with Id {channelId}", ApiHooks.AudioControl);
				return;
			}

			var temp = io.Equals("OUTPUT", StringComparison.OrdinalIgnoreCase)
				? SetAudioOutputLevelRequest
				: SetAudioInputLevelRequest;
			temp?.Invoke(this, new GenericDualEventArgs<string, int>(channelId, level));
		}
		catch (Exception e)
		{
			Logger.Error($"CrComLib.HandlePostLevelRequest() - failed to parse response: {e.Message}");
			SendServerError(ApiHooks.AudioControl);
		}
	}

	private void HandlePostMuteRequest(ResponseBase response)
	{
		try
		{
			var io = response.Data.Value<string>("Io");
			var channelId = response.Data.Value<string>("Id");
			if (string.IsNullOrEmpty(io) || string.IsNullOrEmpty(channelId))
			{
				SendError("Invalid POST mute request - missing Io, Id", ApiHooks.AudioControl);
				return;
			}

			if (!TryFindChannel(io, channelId, out _))
			{
				SendError($"Invalid POST mute request - no channel with Id {channelId}", ApiHooks.AudioControl);
				return;
			}
			
			var temp = io.Equals("OUTPUT", StringComparison.OrdinalIgnoreCase)
				? AudioOutputMuteChangeRequest
				: AudioInputMuteChangeRequest;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(channelId));
		}
		catch (Exception e)
		{
			Logger.Debug($"CrComLib.HandlePostMuteRequest() - failed to parse response: {e.Message}");
			SendServerError(ApiHooks.AudioControl);
		}
	}

	private void HandlePostRouteRequest(ResponseBase response)
	{
		Logger.Debug("CrComLibUi.AudioControlComponent.HandlePostRouteRequest()");
		try
		{
			var outId = response.Data.Value<string>("OutId");
			var inId = response.Data.Value<string>("InId");
			if (string.IsNullOrEmpty(outId) || string.IsNullOrEmpty(inId))
			{
				SendError("Invalid POST route request - missing OutId or InId", ApiHooks.AudioControl);
				return;
			}

			if (!_inputs.Any(x => x.Id.Equals(inId)))
			{
				SendError($"Invalid POST route request - no input found with InId {inId}", ApiHooks.AudioControl);
				return;
			}

			if (!_outputs.Any(x => x.Id.Equals(outId)))
			{
				SendError($"Invalid POST route request - no output found with OutId {outId}", ApiHooks.AudioControl);
				return;
			}

			var temp = AudioOutputRouteRequest;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(inId, outId));
		}
		catch (Exception e)
		{
			Logger.Error($"CrComLib.HandlePostRouteRequest() - failed to parse response: {e.Message}");
			SendServerError(ApiHooks.AudioControl);
		}
	}

	private void HandlePostMicZoneRequest(ResponseBase response)
	{
		Logger.Debug("CrComLibUi.AudioControlComponent.HandlePostMicZoneRequest()");
		try
		{
			var inId = response.Data.Value<string>("InId");
			var zoneId = response.Data.Value<string>("ZoneId");
			if (string.IsNullOrEmpty(inId) || string.IsNullOrEmpty(zoneId))
			{
				SendError("Invalid POST mic zone request - missing InId, ZoneId.", ApiHooks.AudioControl);
				return;
			}
			
			var channel = _inputs.FirstOrDefault(x => x.Id.Equals(inId));
			if (channel == null)
			{
				SendError($"Invalid POST mic zone request - no channel with id {inId}", ApiHooks.AudioControl);
				return;
			}
			
			var zone = channel.Zones.FirstOrDefault(x => x.Id.Equals(zoneId));
			if (zone == null)
			{
				SendError($"Invalid POST mic zone request - no zone on channel {inId} with zone {zoneId}", ApiHooks.AudioControl);
				return;
			}

			var temp = AudioZoneEnableToggleRequest;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(inId, zoneId));
		}
		catch (Exception e)
		{
			Logger.Error($"CrComLib.HandlePostMicZoneRequest() - failed to parse response: {e.Message}");
			SendServerError(ApiHooks.AudioControl);
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
			SendError($"Unsupported GET command: {response.Command}", ApiHooks.AudioControl);
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
			SendError($"Unsupported POST command: {response.Command}", ApiHooks.AudioControl);
		}
	}

	private bool TryFindChannel(string io, string channelId, out AudioChannel? channel)
	{
		channel = io switch
		{
			"INPUT" => _inputs.FirstOrDefault(x => x.Id.Equals(channelId, StringComparison.OrdinalIgnoreCase)),
			"OUTPUT" => _outputs.FirstOrDefault(x => x.Id.Equals(channelId, StringComparison.OrdinalIgnoreCase)),
			_ => null
		};

		return channel != null;
	}
	
	private static List<AudioChannel> CreateChannelCollection(ReadOnlyCollection<AudioChannelInfoContainer> data)
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