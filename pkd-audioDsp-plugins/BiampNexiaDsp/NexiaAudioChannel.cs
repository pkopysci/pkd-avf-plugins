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
		private readonly int hostId;
		private int currentLevel;
		private bool currentMute;

		/// <summary>
		/// Initializes a new instance of the <see cref="QscAudioChannel"/> class.
		/// </summary>
		/// <param name="hostId">the ID of the core that contains the named control.</param>
		/// <param name="deviceId">the unique ID for this channel used for referencing.</param>
		/// <param name="muteTag">The name or number of the mute control block.</param>
		/// <param name="levelTag">The name or number of the level control block.</param>
		public NexiaAudioChannel(int hostId, string deviceId, string muteTag, string levelTag, string[] tags, int index, int levelMax, int levelMin)
		{
			ParameterValidator.ThrowIfNullOrEmpty(deviceId, "Ctor", "deviceId");
			ParameterValidator.ThrowIfNullOrEmpty(muteTag, "Ctor", "muteTag");
			ParameterValidator.ThrowIfNullOrEmpty(levelTag, "Ctor", "levelTag");
			ParameterValidator.ThrowIfNull(tags, "Ctor", "tags");

			this.hostId = hostId;
			this.Id = deviceId;
			this.MuteTag = muteTag;
			this.LevelTag = levelTag;
			this.Tags = tags;
			this.Index = index;
			this.currentLevel = 0;
			this.LevelMax = levelMax;
			this.LevelMin = levelMin;
			this.currentMute = false;
		}

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, int>> AudioMuteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, int>> AudioVolumeChanged;

		/// <summary>
		/// The command used by the parent Nexia DSP control class for adding level control commands
		/// to the send queue.
		/// </summary>
		public Action<string> QueueCommand { get; set; }

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

		/// <inheritdoc/>
		public string Id { get; private set; }

		public int LevelMin { get; private set; }

		public int LevelMax { get; private set; }

		/// <inheritdoc/>
		public IEnumerable<string> Tags { get; private set; }

		/// <inheritdoc/>
		public int AudioLevel
		{
			get
			{
				return this.currentLevel;
			}
			set
			{
				this.currentLevel = value;
				var temp = this.AudioVolumeChanged;
				temp?.Invoke(this, new GenericDualEventArgs<string, int>(this.Id, this.currentLevel));
			}
		}

		/// <inheritdoc/>
		public bool AudioMute
		{
			get
			{
				return this.currentMute;
			}
			set
			{
				this.currentMute = value;
				var temp = this.AudioMuteChanged;
				temp?.Invoke(this, new GenericDualEventArgs<string, int>(this.Id, this.currentMute ? 1 : 0));
			}
		}

		/// <inheritdoc/>
		public void AudioLevelDown()
		{
			this.QueueCommand?.Invoke(string.Format(
					"{0} {1} {2} {3} {4} 1.0\n",
					NexiaComander.Commands[NexiaCommands.DecD],
					this.hostId,
					NexiaComander.Blocks[NexiaBlocks.FaderLevel],
					this.LevelTag,
					this.Index));
		}

		/// <inheritdoc/>
		public void AudioLevelUp()
		{
			this.QueueCommand?.Invoke(string.Format(
					"{0} {1} {2} {3} {4} 1.0\n",
					NexiaComander.Commands[NexiaCommands.IncD],
					this.hostId,
					NexiaComander.Blocks[NexiaBlocks.FaderLevel],
					this.LevelTag,
					this.Index));
		}

		/// <inheritdoc/>
		public void SetAudioLevel(int level)
		{
			Logger.Debug("NexiaAudioChannel {0} - SetAudioLevel({1})", this.Id, level);

			// SETD 1 FDRLVL 13 1 -12\n
			this.QueueCommand?.Invoke(String.Format(
				"{0} {1} {2} {3} {4} {5}\n",
				NexiaComander.Commands[NexiaCommands.SetD],
				this.hostId,
				NexiaComander.Blocks[NexiaBlocks.FaderLevel],
				this.LevelTag,
				this.Index,
				Math.Round(NexiaComander.ConvertToDb(level, this.LevelMin, this.LevelMax), 0)
				));
		}

		/// <inheritdoc/>
		public void SetAudioMute(bool state)
		{
			this.QueueCommand?.Invoke(string.Format(
					"{0} {1} {2} {3} {4} {5}\n",
					NexiaComander.Commands[NexiaCommands.SetD],
					this.hostId,
					NexiaComander.Blocks[NexiaBlocks.FaderMute],
					this.MuteTag,
					this.Index,
					state ? 1 : 0));
		}

		/// <inheritdoc/>
		public void ToggleAudioMute()
		{
			this.QueueCommand?.Invoke(string.Format(
					"{0} {1} {2} {3} {4} {5}\n",
					NexiaComander.Commands[NexiaCommands.SetD],
					this.hostId,
					NexiaComander.Blocks[NexiaBlocks.FaderMute],
					this.MuteTag,
					this.Index,
					this.AudioMute ? 0 : 1));
		}

		/// <summary>
		/// Sends a query command for the current audio level to the QueueCommand action.
		/// </summary>
		public void QueryLevel()
		{
			this.QueueCommand?.Invoke(string.Format(
					"{0} {1} {2} {3} {4}\n",
					NexiaComander.Commands[NexiaCommands.GetD],
					this.hostId,
					NexiaComander.Blocks[NexiaBlocks.FaderLevel],
					this.LevelTag,
					this.Index));
		}

		/// <summary>
		/// Sends a query command for the current mute state to the QueueCommand action.
		/// </summary>
		public void QueryMute()
		{
			this.QueueCommand?.Invoke(string.Format(
					"{0} {1} {2} {3} {4}\n",
					NexiaComander.Commands[NexiaCommands.GetD],
					this.hostId,
					NexiaComander.Blocks[NexiaBlocks.FaderMute],
					this.MuteTag,
					this.Index));
		}
	}
}
