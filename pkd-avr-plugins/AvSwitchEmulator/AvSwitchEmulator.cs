namespace AvSwitchEmulator
{
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_hardware_service.AudioDevices;
	using pkd_hardware_service.AvSwitchDevices;
	using pkd_hardware_service.BaseDevice;
	using pkd_hardware_service.DisplayDevices;
	using System;
	using System.Collections.Generic;

	public class AvSwitchEmulator : BaseDevice, IAvSwitcher, IVideoControllable, IAudioControl
	{
		private readonly Dictionary<uint, uint> _outputs = new();
		private readonly Dictionary<string, AudioChannel> _audioOuts = new();
		private readonly Dictionary<string, AudioChannel> _audioIns = new();
		private int _maxInputs;
		private int _maxOutputs;
		private string _hostname = string.Empty;
		private int _port;

		public AvSwitchEmulator()
		{
			Manufacturer = "Emulator, Inc.";
			Model = "Av-Switch Emulator";
		}
		
		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, uint>>? VideoRouteChanged;
		
		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>>? VideoBlankChanged;
		
		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>>? VideoFreezeChanged;
		
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputLevelChanged;
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputMuteChanged;
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputLevelChanged;
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputMuteChanged;

		/// <inheritdoc/>
		public bool FreezeState { get; private set; }

		/// <inheritdoc/>
		public bool BlankState { get; private set; }

		/// <inheritdoc/>
		public void ClearVideoRoute(uint output)
		{
			_outputs[output] = 0;
		}

		/// <inheritdoc/>
		public void FreezeOff()
		{
			FreezeState = false;
			VideoFreezeChanged?.Invoke(this, new GenericSingleEventArgs<string>(Id));
		}

		/// <inheritdoc/>
		public void FreezeOn()
		{
			FreezeState = true;
			VideoFreezeChanged?.Invoke(this, new GenericSingleEventArgs<string>(Id));
		}

		/// <inheritdoc/>
		public uint GetCurrentVideoSource(uint output)
		{
			return _outputs[output];
		}

		/// <inheritdoc/>
		public void Initialize(string hostName, int port, string id, string label, int numInputs, int numOutputs)
		{
			_hostname = hostName;
			_port = port;
			Id = id;
			Label = label;
			_maxInputs = numInputs;
			_maxOutputs = numOutputs;

			for (uint i = 0; i < numOutputs; i++)
			{
				_outputs.Add(i, 0);
			}
		}

		/// <inheritdoc/>
		public void RouteVideo(uint source, uint output)
		{
			_outputs[output] = source;
			var temp = VideoRouteChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, uint>(Id, output));
		}

		/// <inheritdoc/>
		public void VideoBlankOff()
		{
			BlankState = false;
			VideoBlankChanged?.Invoke(this, new GenericSingleEventArgs<string>(Id));
		}

		/// <inheritdoc/>
		public void VideoBlankOn()
		{
			BlankState = true;
			VideoBlankChanged?.Invoke(this, new GenericSingleEventArgs<string>(Id));
		}

		/// <inheritdoc/>
		public override void Connect()
		{
			Logger.Debug($"AvSwitchEmulator: Connecting to {_hostname}:{_port}.");
			Logger.Debug($"MaxInputs: {_maxInputs}, MaxOutputs: {_maxOutputs}");
			IsOnline = true;
			NotifyOnlineStatus();
		}

		/// <inheritdoc/>
		public override void Disconnect()
		{
			IsOnline = false;
			NotifyOnlineStatus();
		}

		public IEnumerable<string> GetAudioPresetIds()
		{
			return new List<string>();	
		}

		public IEnumerable<string> GetAudioInputIds()
		{
			var ids = new List<string>();
			foreach (var key in _audioIns.Keys) { ids.Add(key); }
			return ids;
		}

		public IEnumerable<string> GetAudioOutputIds()
		{
			var ids = new List<string>();
			foreach (var key in _audioOuts.Keys) { ids.Add(key); }
			return ids;
		}

		public void SetAudioInputLevel(string id, int level)
		{
			if (_audioIns.TryGetValue(id, out var channel))
			{
				channel.CurrentLevel = level;
				AudioInputLevelChanged?.Invoke(
					this,
					new GenericDualEventArgs<string, string>(Id, channel.Id));
			}
		}

		public int GetAudioInputLevel(string id)
		{
			if (_audioIns.TryGetValue(id, out var channel))
			{
				return channel.CurrentLevel;
			}
			else
			{
				return 0;
			}
		}

		public void SetAudioInputMute(string id, bool mute)
		{
			if (_audioIns.TryGetValue(id, out var channel))
			{
				channel.MuteState = mute;
				AudioInputMuteChanged?.Invoke(this, new GenericDualEventArgs<string, string>(Id, channel.Id));
			}
		}

		public bool GetAudioInputMute(string id)
		{
			if (_audioIns.TryGetValue(id, out var channel))
			{
				return channel.MuteState;
			}
			else
			{
				return false;
			}
		}

		public void SetAudioOutputLevel(string id, int level)
		{
			if (_audioOuts.TryGetValue(id, out var channel))
			{
				channel.CurrentLevel = level;
				AudioOutputLevelChanged?.Invoke(
					this,
					new GenericDualEventArgs<string, string>(Id, channel.Id));
			}
		}

		public int GetAudioOutputLevel(string id)
		{
			if (_audioOuts.TryGetValue(id, out var channel))
			{
				return channel.CurrentLevel;
			}
			else
			{
				return 0;
			}
		}

		public void SetAudioOutputMute(string id, bool mute)
		{
			if (_audioOuts.TryGetValue(id, out var channel))
			{
				channel.MuteState = mute;
				AudioOutputMuteChanged?.Invoke(this, new GenericDualEventArgs<string, string>(Id, channel.Id));
			}
		}

		public bool GetAudioOutputMute(string id)
		{
			if (_audioOuts.TryGetValue(id, out var channel))
			{
				return channel.MuteState;
			}
			else
			{
				return false;
			}
		}

		public void RecallAudioPreset(string id)
		{
			Logger.Debug($"AvSwitchEmulator {Id} - RecallAudioPreset({id})");
		}

		public void AddInputChannel(string id, string levelTag, string muteTag, int bankIndex, int levelMax, int levelMin, int routerIndex, List<string> tags)
		{
			_audioIns.Add(id,
				new AudioChannel()
				{
					Id = id,
					LevelTag = levelTag,
					MuteTag = muteTag,
					BankIndex = bankIndex,
					LevelMax = levelMax,
					LevelMin = levelMin,
					RouterIndex = routerIndex
				});
		}

		public void AddOutputChannel(string id, string levelTag, string muteTag, string routerTag, int routerIndex, int bankIndex, int levelMax, int levelMin, List<string> tags)
		{
			_audioOuts.Add(id,
				new AudioChannel()
				{
					Id = id,
					LevelTag = levelTag,
					MuteTag = muteTag,
					BankIndex = bankIndex,
					LevelMax = levelMax,
					LevelMin = levelMin,
					RouterIndex = routerIndex
				});
		}

		public void AddPreset(string id, int index)
		{
			Logger.Debug($"AvSwitchEmulator {Id} - AddPreset({id}, {index})");
		}
	}
}
