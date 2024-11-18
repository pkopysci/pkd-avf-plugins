namespace QscDsp
{
	using Crestron.SimplSharp;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_common_utils.Validation;
	using QscQsys;
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Audio channel implementation for use with the QscDsp control class.
	/// </summary>
	internal class QscAudioChannel
	{
		private readonly QsysNamedControl levelControl;
		private readonly QsysNamedControl muteControl;
		private readonly QsysNamedControl routerControl;
		private readonly QscZoneEnable zoneEnables;
		private readonly string coreId;
		private readonly string muteTag;
		private readonly string levelTag;
		private readonly string routerTag;
		private uint currentAudioSource;

		/// <summary>
		/// Initializes a new instance of the <see cref="QscAudioChannel"/> class.
		/// </summary>
		/// <param name="coreId">the ID of the core that contains the named control.</param>
		/// <param name="deviceId">the unique ID for this channel used for referencing.</param>
		/// <param name="muteTag">The named control for muting this channel.</param>
		/// <param name="levelTag">The named control for adjust level of this channel.</param>
		public QscAudioChannel(string coreId, string deviceId, string muteTag, string levelTag, string[] tags, int routerIndex)
		{
			ParameterValidator.ThrowIfNullOrEmpty(coreId, "QscAudioChannel.Ctor", "coreId");
			ParameterValidator.ThrowIfNullOrEmpty(deviceId, "QscAudioChannel.Ctor", "deviceId");
			ParameterValidator.ThrowIfNullOrEmpty(muteTag, "QscAudioChannel.Ctor", "muteTag");
			ParameterValidator.ThrowIfNullOrEmpty(levelTag, "QscAudioChannel.Ctor", "levelTag");
			ParameterValidator.ThrowIfNull(tags, "QscAudioChannel.Ctor", "tags");

			this.Id = deviceId;
			this.Tags = tags;
			this.muteTag = muteTag;
			this.levelTag = levelTag;
			this.coreId = coreId;
			this.RouterIndex = routerIndex;

			this.levelControl = new QsysNamedControl
			{
				newNamedControlUIntChange = this.LevelControlChanged
			};

			this.muteControl = new QsysNamedControl
			{
				newNamedControlIntChange = this.MuteControlChanged
			};

			this.zoneEnables = new QscZoneEnable();
			this.zoneEnables.ZoneControlChanged += this.ZoneEnableChangeHandler;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="QscAudioChannel"/> class.
		/// </summary>
		/// <param name="coreId">the ID of the core that contains the named control.</param>
		/// <param name="deviceId">the unique ID for this channel used for referencing.</param>
		/// <param name="muteTag">The named control for muting this channel.</param>
		/// <param name="routerTag">The named control for routing audio inputs.</param>
		/// <param name="levelTag">The named control for adjust level of this channel.</param>
		public QscAudioChannel(string coreId, string deviceId, string muteTag, string levelTag, string routerTag, string[] tags, int routerIndex)
			: this(coreId, deviceId, muteTag, levelTag, tags, routerIndex)
		{
			ParameterValidator.ThrowIfNullOrEmpty(coreId, "QscAudioChannel.Ctor", "routerTag");
			this.routerTag = routerTag;
			this.routerControl = new QsysNamedControl
			{
				newNamedControlStringChange = this.RouterControlChanged
			};
		}


		/// <summary>
		/// Triggered when a change in audio mute is reported by the QSC hardware.
		/// arg 1 = ID of this channel, arg 2 = new mute state.
		/// </summary>
		public event EventHandler<GenericDualEventArgs<string, int>> AudioMuteChanged;

		/// <summary>
		/// Triggered when a change in volume level is reported by the QSC hardware.
		/// arg1 = the ID of this channel, arg2 = new level state.
		/// </summary>
		public event EventHandler<GenericDualEventArgs<string, int>> AudioVolumeChanged;

		/// <summary>
		/// Triggered whenever the internal router reports a change.
		/// </summary>
		public event EventHandler<GenericDualEventArgs<string, uint>> AudioRouteChanged;

		/// <summary>
		/// Triggered whenever the internal zone enable monitor detects a change from the core.
		/// Will not be triggered if no audio enable zones have been added.
		/// </summary>
		public event EventHandler<GenericDualEventArgs<string, string>> ZoneEnableChanged;

		/// <summary>
		/// Gets the unique ID of this audio channel.
		/// </summary>
		public string Id { get; private set; }

		/// <summary>
		/// Gets a collection of tags that set for this channel at instantiation.
		/// </summary>
		public IEnumerable<string> Tags { get; private set; }

		/// <summary>
		/// Gets the audio level of this channel as of the last reported change from the device.
		/// </summary>
		public int AudioLevel { get; private set; }

		/// <summary>
		/// Gets the audio mute state for this channel as of the last reported change from the device.
		/// </summary>
		public bool AudioMute { get; private set; }

		/// <summary>
		/// The input or output index on the router blockassociated with this channel.
		/// The value will be 0 if not set during instantiation.
		/// </summary>
		public int RouterIndex { get; private set; }

		/// <summary>
		/// Gets the currently routed source if this is an output channel, otherwise 0.
		/// </summary>
		public uint AudioSource
		{
			get
			{
				return this.currentAudioSource;
			}
		}

		/// <summary>
		/// Initializes the internal comunication objects and registers them with the core.
		/// </summary>
		public void Register()
		{
			this.levelControl.Initialize(this.coreId, this.levelTag, 1);
			this.muteControl.Initialize(this.coreId, this.muteTag, 1);
			this.zoneEnables.Register(this.coreId);

			if (!string.IsNullOrEmpty(this.routerTag) && this.routerControl != null)
			{
				this.routerControl.Initialize(this.coreId, this.routerTag, 0);
			}

		}

		/// <summary>
		/// Lower the audio level by one step. Does nothing if this channel has not been registered with
		/// the hardware.
		/// </summary>
		public void AudioLevelDown()
		{
			if (!this.CheckRegistered("AudioLevelDown()", this.levelControl))
			{
				return;
			}

			int tempLevel = this.AudioLevel;
			this.levelControl.SetUnsignedInteger((ushort)(tempLevel - 3), 1);
		}

		/// <summary>
		/// Increase the audio level by one step. Does nothing if this channel has not been registered with
		/// the hardware.
		/// </summary>
		public void AudioLevelUp()
		{
			if (!this.CheckRegistered("AudioLevelUp()", this.levelControl))
			{
				return;
			}

			int tempLevel = this.AudioLevel;
			this.levelControl.SetUnsignedInteger((ushort)(tempLevel + 3), 1);
		}

		/// <summary>
		/// Sets the audio level of this channel to the given value. Does nothing if this channel has not been
		/// registered with the hardware.
		/// </summary>
		/// <param name="level">The target volume level to set.</param>
		public void SetAudioLevel(int level)
		{
			if (!this.CheckRegistered("SetAudioLevel()", this.levelControl))
			{
				return;
			}

			this.levelControl.SetUnsignedInteger((ushort)level, 1);
		}

		/// <summary>
		/// Sets the mute state of this channel to the given vale. DOes nothing if this channel has not been
		/// registered with the hardware.
		/// </summary>
		/// <param name="state">true = mute on, false = mute off</param>
		public void SetAudioMute(bool state)
		{
			if (!this.CheckRegistered("SetAudioMute()", this.muteControl))
			{
				return;
			}

			this.muteControl.SetBoolean(state ? 1 : 0);
		}

		/// <summary>
		/// Send a route command to the hardware if this is an audio output.
		/// Does nothing if the internal router was not defined at instantiation.
		/// </summary>
		/// <param name="inputIndex">the index of the input to attempt to route.</param>
		public void SetAudioRoute(uint inputIndex)
		{
			if (this.routerControl == null || string.IsNullOrEmpty(this.routerTag))
			{
				return;
			}

			this.routerControl.SetString(inputIndex.ToString());
		}

		/// <summary>
		/// Change the audio state to the inverse of what it is currently set to. Does nothing if this cannel has
		/// been registered with the hardware.
		/// </summary>
		public void ToggleAudioMute()
		{
			if (!this.CheckRegistered("ToggleAudioMute()", this.muteControl))
			{
				return;
			}

			this.muteControl.SetBoolean(this.AudioMute ? 0 : 1);
		}

		/// <summary>
		/// Add a zone audio enable/disable object to the internal control & tracking.
		/// </summary>
		/// <param name="zoneId">The unique ID of the zone to add.</param>
		/// <param name="controlTag">The DSP design named control used when changing zone enable state.</param>
		public void AddZoneEnable(string zoneId, string controlTag)
		{
			if (!this.zoneEnables.TryAddZone(zoneId, controlTag))
			{
				Logger.Error("QSC Audio channel {0} - Failed to add zone enable {1}", this.Id, zoneId);
			}
		}

		/// <summary>
		/// Remove an existing audio enable/disable object from the internal tracking collection.
		/// </summary>
		/// <param name="zoneId">The unique ID of the zone enable to remove.</param>
		public void RemoveZoneEnable(string zoneId)
		{
			if (!this.zoneEnables.TryRemoveZone(zoneId))
			{
				Logger.Error("QSC Audio Channel {0} - Failed to remove zone enable {1}", this.Id, zoneId);
			}
		}

		/// <summary>
		/// Send a command to the control object to switch the zone enable state.
		/// </summary>
		/// <param name="zoneId">The unique ID of the zone enable control to adjust.</param>
		public void ToggleZoneEnableState(string zoneId)
		{
			this.zoneEnables.ToggleZone(zoneId);
		}

		/// <summary>
		/// Query the DSP device for the current state of the zone enable control object.
		/// </summary>
		/// <param name="zoneId">The unique ID of the zone to query.</param>
		/// <returns>the current state of the control object (true = enabled/unmuted, false = disabled/muted). Returns false if the object was not found.</returns>
		public bool QueryZoneEnableState(string zoneId)
		{
			return this.zoneEnables.QueryZone(zoneId);
		}

		private void LevelControlChanged(SimplSharpString stringData, ushort shortData)
		{
			Logger.Debug("QscAudioChannel {0} - LevelControlChanged({1}, {2})", this.Id, shortData, stringData);

			this.AudioLevel = (int)shortData;
			var temp = this.AudioVolumeChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, int>(this.Id, this.AudioLevel));
		}

		private void MuteControlChanged(SimplSharpString stringData, short shortData)
		{
			Logger.Debug("QscAudioChannel {0} - MuteControlChanged({1}, {2})", this.Id, shortData, stringData);

			this.AudioMute = shortData > 0;
			var temp = this.AudioMuteChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, int>(this.Id, this.AudioLevel));
		}

		private void RouterControlChanged(SimplSharpString name, SimplSharpString stringData)
		{
			Logger.Debug("QscAudioChannel {0} - RouterControlChanged({1}, {1})", this.Id, name, stringData);

			try
			{
				this.currentAudioSource = uint.Parse(stringData.ToString());
				var temp = this.AudioRouteChanged;
				temp?.Invoke(this, new GenericDualEventArgs<string, uint>(this.Id, this.currentAudioSource));
			}
			catch (Exception e)
			{
				Logger.Error(e, "QscAudioChannel.RouterControlChanged - failed to parse number from response.");
			}
		}

		private bool CheckRegistered(string methodName, QsysNamedControl control)
		{
			if (control.IsRegistered)
			{
				return true;
			}

			Logger.Error(
				"QscAudioChannel.{0}() - Named control {1} is not registered on DSP {2}",
				methodName,
				control.ComponentName,
				this.coreId);

			return false;
		}

		private void ZoneEnableChangeHandler(object sender, GenericSingleEventArgs<string> e)
		{
			var temp = this.ZoneEnableChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, e.Arg));
		}
	}
}
