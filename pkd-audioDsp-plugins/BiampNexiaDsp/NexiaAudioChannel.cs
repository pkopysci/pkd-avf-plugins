namespace BiampNexiaDsp
{
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_common_utils.Validation;
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Audio channel implementation for use with a Biamp Nexia control class.
	/// </summary>
	internal class NexiaAudioChannel
	{
		private readonly int _hostId;
		private int _currentLevel;
		private bool _currentMute;

		/// <summary>
		/// Initializes a new instance of the <see cref="NexiaAudioChannel"/> class.
		/// </summary>
		/// <param name="hostId">the ID of the core that contains the named control.</param>
		/// <param name="deviceId">the unique ID for this channel used for referencing.</param>
		/// <param name="muteTag">The name or number of the mute control block.</param>
		/// <param name="levelTag">The name or number of the level control block.</param>
		/// <param name="tags">A collection of tags for the DSP device defined during system configuration.</param>
		/// <param name="index">The index of this channel in the control group/bank.</param>
		/// <param name="levelMax">The maximum allowed gain level for the channel.</param>
		/// <param name="levelMin">The minimum allowed gain level for the channel.</param>
		public NexiaAudioChannel(int hostId, string deviceId, string muteTag, string levelTag, string[] tags, int index, int levelMax, int levelMin)
		{
			ParameterValidator.ThrowIfNullOrEmpty(deviceId, "Ctor", "deviceId");
			ParameterValidator.ThrowIfNullOrEmpty(muteTag, "Ctor", "muteTag");
			ParameterValidator.ThrowIfNullOrEmpty(levelTag, "Ctor", "levelTag");
			ParameterValidator.ThrowIfNull(tags, "Ctor", "tags");

			_hostId = hostId;
			_currentLevel = 0;
			_currentMute = false;
			Id = deviceId;
			MuteTag = muteTag;
			LevelTag = levelTag;
			Tags = tags;
			Index = index;
			LevelMax = levelMax;
			LevelMin = levelMin;
		}

		/// <summary>
		/// Triggered when a state change is detected on the internal mute control connection.
		/// </summary>
		public event EventHandler<GenericDualEventArgs<string, int>>? AudioMuteChanged;

		/// <summary>
		/// Triggered when a level change is detected on the internal gain control connection.
		/// </summary>
		public event EventHandler<GenericDualEventArgs<string, int>>? AudioVolumeChanged;

		/// <summary>
		/// The command used by the parent Nexia DSP control class for adding level control commands
		/// to the send queue.
		/// </summary>
		public Action<string>? QueueCommand { get; set; }

		/// <summary>
		/// Gets the tag or number for level control that was set at instantiation.
		/// </summary>
		public string LevelTag { get; private set; }

		/// <summary>
		/// Gets the tag or number for mute control that was set at instantiation.
		/// </summary>
		public string MuteTag { get; private set; }

		/// <summary>
		/// The index of the channel to control. Single channels are 1 but some channel controls are on
		/// a block of multiple channels.
		/// </summary>
		public int Index { get; private set; }

		/// <summary>
		/// The unique id of this channel. This is used for internal referencing.
		/// </summary>
		public string Id { get; private set; }

		/// <summary>
		/// The minimum gain/volume level that is allowed by the DSP design. This is set during instantiation.
		/// </summary>
		public int LevelMin { get; private set; }

		/// <summary>
		/// The maximum gain/volume level that is allowed by the DSP design. This is set during instantiation.
		/// </summary>
		public int LevelMax { get; private set; }

		/// <summary>
		/// A collection of custom tags assigned to this channel. These are used by a parent object for any custom behavior that is needed.
		/// </summary>
		public IEnumerable<string> Tags { get; private set; }

		/// <summary>
		/// The current audio level as of the last update. This value is not scaled internally at all.
		/// </summary>
		public int AudioLevel
		{
			get => _currentLevel;
			set
			{
				_currentLevel = value;
				var temp = AudioVolumeChanged;
				temp?.Invoke(this, new GenericDualEventArgs<string, int>(Id, _currentLevel));
			}
		}

		/// <summary>
		/// The current mute state of this audio channel. true = mute active (no audio), false = mute not active (passing audio).
		/// </summary>
		public bool AudioMute
		{
			get => _currentMute;
			set
			{
				_currentMute = value;
				var temp = AudioMuteChanged;
				temp?.Invoke(this, new GenericDualEventArgs<string, int>(Id, _currentMute ? 1 : 0));
			}
		}

		/// <summary>
		/// Queue a command that will decrease the audio level of this channel.
		/// </summary>
		public void AudioLevelDown()
		{
			QueueCommand?.Invoke(string.Format(
					"{0} {1} {2} {3} {4} 1.0\n",
					NexiaCommander.Commands[NexiaCommands.DecD],
					_hostId,
					NexiaCommander.Blocks[NexiaBlocks.FaderLevel],
					LevelTag,
					Index));
		}

		/// <summary>
		/// Queue a command that will increase the audio level of this channel.
		/// </summary>
		public void AudioLevelUp()
		{
			QueueCommand?.Invoke(string.Format(
					"{0} {1} {2} {3} {4} 1.0\n",
					NexiaCommander.Commands[NexiaCommands.IncD],
					_hostId,
					NexiaCommander.Blocks[NexiaBlocks.FaderLevel],
					LevelTag,
					Index));
		}

		/// <summary>
		/// Queue a command that will set the audio level of this channel to a specific value. This will adjust the provided argument
		/// to stay within the LevelMin and LevelMax bounds.
		/// </summary>
		/// <param name="level">The target level to set the channel gain. Must be within LevelMin and LevelMax.</param>
		public void SetAudioLevel(int level)
		{
			Logger.Debug("NexiaAudioChannel {0} - SetAudioLevel({1})", Id, level);

			// SETD 1 FDRLVL 13 1 -12\n
			QueueCommand?.Invoke(String.Format(
				"{0} {1} {2} {3} {4} {5}\n",
				NexiaCommander.Commands[NexiaCommands.SetD],
				_hostId,
				NexiaCommander.Blocks[NexiaBlocks.FaderLevel],
				LevelTag,
				Index,
				Math.Round(NexiaCommander.ConvertToDb(level, LevelMin, LevelMax), 0)
				));
		}

		/// <summary>
		/// Queue a command to discretely set the mute state of this channel.
		/// </summary>
		/// <param name="state">true = set mute active (no audio), false = set mute not active (pass audio).</param>
		public void SetAudioMute(bool state)
		{
			QueueCommand?.Invoke(string.Format(
					"{0} {1} {2} {3} {4} {5}\n",
					NexiaCommander.Commands[NexiaCommands.SetD],
					_hostId,
					NexiaCommander.Blocks[NexiaBlocks.FaderMute],
					MuteTag,
					Index,
					state ? 1 : 0));
		}

		/// <summary>
		/// Queue a command to toggle the current mute state of this channel.
		/// </summary>
		public void ToggleAudioMute()
		{
			QueueCommand?.Invoke(string.Format(
					"{0} {1} {2} {3} {4} {5}\n",
					NexiaCommander.Commands[NexiaCommands.SetD],
					_hostId,
					NexiaCommander.Blocks[NexiaBlocks.FaderMute],
					MuteTag,
					Index,
					AudioMute ? 0 : 1));
		}

		/// <summary>
		/// Queue a query command for the current audio level to the QueueCommand action.
		/// </summary>
		public void QueryLevel()
		{
			QueueCommand?.Invoke(string.Format(
					"{0} {1} {2} {3} {4}\n",
					NexiaCommander.Commands[NexiaCommands.GetD],
					_hostId,
					NexiaCommander.Blocks[NexiaBlocks.FaderLevel],
					LevelTag,
					Index));
		}

		/// <summary>
		/// Queue a query command for the current mute state to the QueueCommand action.
		/// </summary>
		public void QueryMute()
		{
			QueueCommand?.Invoke(string.Format(
					"{0} {1} {2} {3} {4}\n",
					NexiaCommander.Commands[NexiaCommands.GetD],
					_hostId,
					NexiaCommander.Blocks[NexiaBlocks.FaderMute],
					MuteTag,
					Index));
		}
	}
}
