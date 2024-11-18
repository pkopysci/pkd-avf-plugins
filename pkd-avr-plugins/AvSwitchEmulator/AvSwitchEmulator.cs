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
		private readonly Dictionary<uint, uint> outputs;
		private readonly Dictionary<string, AudioChannel> audioOuts;
		private readonly Dictionary<string, AudioChannel> audioIns;
		private int maxInputs;
		private int maxOutputs;
		private string hostname;
		private int port;
		private string username;
		private string password;

		public AvSwitchEmulator()
		{
			this.outputs = new Dictionary<uint, uint>();
			this.audioOuts = new Dictionary<string, AudioChannel>();
			this.audioIns = new Dictionary<string, AudioChannel>();
		}

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, uint>> VideoRouteChanged;
		
		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> VideoBlankChanged;
		
		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> VideoFreezeChanged;
		
		public event EventHandler<GenericDualEventArgs<string, string>> AudioInputLevelChanged;
		public event EventHandler<GenericDualEventArgs<string, string>> AudioInputMuteChanged;
		public event EventHandler<GenericDualEventArgs<string, string>> AudioOutputLevelChanged;
		public event EventHandler<GenericDualEventArgs<string, string>> AudioOutputMuteChanged;

		/// <inheritdoc/>
		public bool FreezeState { get; private set; }

		/// <inheritdoc/>
		public bool BlankState { get; private set; }

		/// <inheritdoc/>
		public void ClearVideoRoute(uint output)
		{
			this.outputs[output] = 0;
		}

		/// <inheritdoc/>
		public void FreezeOff()
		{
			this.FreezeState = false;
			this.VideoFreezeChanged?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}

		/// <inheritdoc/>
		public void FreezeOn()
		{
			this.FreezeState = true;
			this.VideoFreezeChanged?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}

		/// <inheritdoc/>
		public uint GetCurrentVideoSource(uint output)
		{
			return this.outputs[output];
		}

		/// <inheritdoc/>
		public void Initialize(string hostName, int port, string id, string label, int numInputs, int numOutputs)
		{
			this.hostname = hostName;
			this.port = port;
			this.Id = id;
			this.Label = label;
			this.maxInputs = numInputs;
			this.maxOutputs = numOutputs;

			for (uint i = 0; i < numOutputs; i++)
			{
				this.outputs.Add(i, 0);
			}
		}

		/// <inheritdoc/>
		public void RouteVideo(uint source, uint output)
		{
			this.outputs[output] = source;
			var temp = this.VideoRouteChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, uint>(this.Id, output));
		}

		/// <inheritdoc/>
		public void VideoBlankOff()
		{
			this.BlankState = false;
			this.VideoBlankChanged?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}

		/// <inheritdoc/>
		public void VideoBlankOn()
		{
			this.BlankState = true;
			this.VideoBlankChanged?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}

		/// <inheritdoc/>
		public override void Connect()
		{
			this.IsOnline = true;
			this.NotifyOnlineStatus();
		}

		/// <inheritdoc/>
		public override void Disconnect()
		{
			this.IsOnline = false;
			this.NotifyOnlineStatus();
		}

		public IEnumerable<string> GetAudioPresetIds()
		{
			return new List<string>();	
		}

		public IEnumerable<string> GetAudioInputIds()
		{
			var ids = new List<string>();
			foreach (var key in this.audioIns.Keys) { ids.Add(key); }
			return ids;
		}

		public IEnumerable<string> GetAudioOutputIds()
		{
			var ids = new List<string>();
			foreach (var key in this.audioOuts.Keys) { ids.Add(key); }
			return ids;
		}

		public void SetAudioInputLevel(string id, int level)
		{
			if (this.audioIns.TryGetValue(id, out var channel))
			{
				channel.CurrentLevel = level;
				this.AudioInputLevelChanged?.Invoke(
					this,
					new GenericDualEventArgs<string, string>(this.Id, channel.Id));
			}
		}

		public int GetAudioInputLevel(string id)
		{
			if (this.audioIns.TryGetValue(id, out var channel))
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
			if (this.audioIns.TryGetValue(id, out var channel))
			{
				channel.MuteState = mute;
				this.AudioInputMuteChanged?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, channel.Id));
			}
		}

		public bool GetAudioInputMute(string id)
		{
			if (this.audioIns.TryGetValue(id, out var channel))
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
			if (this.audioOuts.TryGetValue(id, out var channel))
			{
				channel.CurrentLevel = level;
				this.AudioOutputLevelChanged?.Invoke(
					this,
					new GenericDualEventArgs<string, string>(this.Id, channel.Id));
			}
		}

		public int GetAudioOutputLevel(string id)
		{
			if (this.audioOuts.TryGetValue(id, out var channel))
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
			if (this.audioOuts.TryGetValue(id, out var channel))
			{
				channel.MuteState = mute;
				this.AudioOutputMuteChanged?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, channel.Id));
			}
		}

		public bool GetAudioOutputMute(string id)
		{
			if (this.audioOuts.TryGetValue(id, out var channel))
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
			Logger.Debug($"AvSwitchEmulator {this.Id} - RecallAudioPreset({id})");
		}

		public void AddInputChannel(string id, string levelTag, string muteTag, int bankIndex, int levelMax, int levelMin, int routerIndex)
		{
			this.audioIns.Add(id,
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

		public void AddOutputChannel(string id, string levelTag, string muteTag, string routerTag, int routerIndex, int bankIndex, int levelMax, int levelMin)
		{
			this.audioOuts.Add(id,
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
			Logger.Debug($"AvSwitchEmulator {this.Id} - AddPreset({id}, {index})");
		}
	}
}
