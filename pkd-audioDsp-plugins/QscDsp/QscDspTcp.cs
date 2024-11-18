namespace QscDsp
{
	using Crestron.SimplSharp;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_common_utils.Validation;
	using pkd_hardware_service.AudioDevices;
	using pkd_hardware_service.BaseDevice;
	using pkd_hardware_service.Routable;
	using QscQsys;
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// QSC control implementation for DSP control.
	/// Uses QscQsys depenency library for tx/rx, devolped by Mat Klucznyk:
	/// https://github.com/MatKlucznyk/Qsys.
	/// </summary>
	public class QscDspTcp : BaseDevice, IDsp, IAudioRoutable, IDisposable, IAudioZoneEnabler
	{
		private bool disposed;
		private readonly List<QscAudioChannel> inputs;
		private readonly List<QscAudioChannel> outputs;
		private readonly List<QscSnapshotBank> snapshots;
		private bool coreRegistered;
		private string hostname;
		private string username;
		private string password;
		private int port;
		private QsysCore core;

		/// <summary>
		/// Initializes a new instance of the <see cref="QscDsp"/> class.
		/// </summary>
		public QscDspTcp()
		{
			this.IsInitialized = false;
			this.Id = "QscDspDefaultID";
			this.Label = "QSC DSP";
			this.inputs = new List<QscAudioChannel>();
			this.outputs = new List<QscAudioChannel>();
			this.snapshots = new List<QscSnapshotBank>();
		}

		~QscDspTcp()
		{
			this.Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioInputLevelChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioInputMuteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioOutputLevelChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioOutputMuteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioRouteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AudioZoneEnableChanged;

		/// <inheritdoc/>
		public void AddInputChannel(string id, string levelTag, string muteTag, int bankIndex, int levelMax, int levelMin, int routerIndex)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "QscDspTcp.AddInputChannel", "id");
			ParameterValidator.ThrowIfNullOrEmpty(levelTag, "QscDspTcp.AddInputChannel", "levelTag");
			ParameterValidator.ThrowIfNullOrEmpty(muteTag, "QscDspTcp.AddInputChannel", "muteTag");
			if (bankIndex < 0)
			{
				throw new ArgumentException("QscDspTcp.AddInputChannel() - 'bankIndex' cannot be less than 0.");
			}

			var existing = this.inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (existing != null)
			{
				existing.AudioMuteChanged -= this.InputControlMuteChange;
				existing.AudioVolumeChanged -= this.InputControlLevelChange;
				this.inputs.Remove(existing);
			}

			QscAudioChannel channel = new QscAudioChannel(this.Id, id, muteTag, levelTag, new string[] { }, routerIndex);
			channel.AudioMuteChanged += this.InputControlMuteChange;
			channel.AudioVolumeChanged += this.InputControlLevelChange;
			channel.ZoneEnableChanged += this.ZoneEnableChangedHandler;
			this.inputs.Add(channel);
		}

		/// <inheritdoc/>
		public void AddOutputChannel(string id, string levelTag, string muteTag, string routerTag, int routerIndex, int bankIndex, int levelMax, int levelMin)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "QscDspTcp.AddOutputChannel", "id");
			ParameterValidator.ThrowIfNullOrEmpty(levelTag, "QscDspTcp.AddOutputChannel", "ilevelTagd");
			ParameterValidator.ThrowIfNullOrEmpty(muteTag, "QscDspTcp.AddOutputChannel", "muteTag");
			if (bankIndex < 0)
			{
				throw new ArgumentException("QscDspTcp.AddOutputChannel() - 'bankIndex' cannot be less than 0.");
			}


			var existing = this.outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (existing != null)
			{
				existing.AudioVolumeChanged -= this.OutputControlLevelChange;
				existing.AudioMuteChanged -= this.OutputControlMuteChange;
				this.outputs.Remove(existing);
			}

			QscAudioChannel channel = new QscAudioChannel(this.Id, id, muteTag, levelTag, routerTag, new String[] { }, routerIndex);
			channel.AudioMuteChanged += this.OutputControlMuteChange;
			channel.AudioVolumeChanged += this.OutputControlLevelChange;
			channel.AudioRouteChanged += this.OutputControlRouteChanged;
			channel.ZoneEnableChanged += this.ZoneEnableChangedHandler;
			this.outputs.Add(channel);
		}

		/// <inheritdoc/>
		public void AddPreset(string id, int index)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "QscDspTcp.AddPreset", "id");
			if (index < 0)
			{
				throw new ArgumentException("QscDspTcp.AddPreset() - 'index' cannot be less than zero.");
			}

			var existing = this.snapshots.Find(x => x.Name.Equals(id, StringComparison.InvariantCulture));
			if (existing == null)
			{
				existing = new QscSnapshotBank(this.Id, id);
				this.snapshots.Add(existing);
			}

			string presetId = string.Format("{0}.{1}", id, index);
			existing.AddPreset(presetId, index);
			if (!existing.IsRegistered)
			{
				existing.Register();
			}
		}

		/// <inheritdoc/>
		public void Initialize(string hostId, int coreId, string hostname, int port, string username, string password)
		{
			ParameterValidator.ThrowIfNullOrEmpty(hostId, "Initialize", "coreId");
			ParameterValidator.ThrowIfNullOrEmpty(hostname, "Initialize", "hostname");
			ParameterValidator.ThrowIfNull(username, "Initialize", "username");
			ParameterValidator.ThrowIfNull(password, "Initialize", "password");

			this.IsInitialized = false;
			this.Id = hostId;
			this.hostname = hostname;
			this.port = port;
			this.username = username;
			this.password = password;
			this.IsInitialized = true;
		}

		/// <inheritdoc/>
		public override void Connect()
		{
			if (this.IsInitialized)
			{
				this.core = new QsysCore();

#if DEBUG
				this.core.Debug(1);
#endif
				this.core.onIsConnected = this.OnCoreConnected;
				this.core.onNewCoreStatus += this.OnCoreStatusChange;
				this.core.onIsRegistered += this.OnCoreRegistered;

				this.core.Initialize(
					this.Id,
					this.hostname,
					(ushort)this.port,
					this.username,
					this.password,
					0);
			}
			else
			{
				Logger.Error("QscDspTcp {0} - Cannot connect. Run Initialize() first.", this.Id);
			}
		}

		/// <inheritdoc/>
		public override void Disconnect()
		{
			if (this.core != null)
			{
				this.core.Dispose();
				this.core = null;
				this.coreRegistered = false;
				this.NotifyOnlineStatus();
			}
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioInputIds()
		{
			return this.inputs.Select(x => x.Id).ToList();
		}

		/// <inheritdoc/>
		public int GetAudioInputLevel(string id)
		{
			var found = this.inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found == null)
			{
				return 0;
			}

			return this.ConvertLevelToPercent(found.AudioLevel);
		}

		/// <inheritdoc/>
		public bool GetAudioInputMute(string id)
		{
			var found = this.inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found == null)
			{
				return false;
			}

			return found.AudioMute;
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioOutputIds()
		{
			return this.outputs.Select(x => x.Id).ToList();
		}

		/// <inheritdoc/>
		public int GetAudioOutputLevel(string id)
		{
			var found = this.outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found == null)
			{
				return 0;
			}

			return this.ConvertLevelToPercent(found.AudioLevel);
		}

		/// <inheritdoc/>
		public bool GetAudioOutputMute(string id)
		{
			var found = this.outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found == null)
			{
				return false;
			}

			return found.AudioMute;
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioPresetIds()
		{
			List<string> presetIds = new List<string>();
			foreach (var bank in this.snapshots)
			{
				foreach (var id in bank.PresetIds)
				{
					presetIds.Add(id);
				}
			}

			return presetIds;
		}

		/// <inheritdoc/>
		public void RecallAudioPreset(string id)
		{
			foreach (var bank in this.snapshots)
			{
				if (bank.HasPreset(id))
				{
					Logger.Debug("QscDspTcp.RecallAudioPreset({0})", id);

					bank.RecallPreset(id);
					break;
				}
			}
		}

		/// <inheritdoc/>
		public void SetAudioInputLevel(string id, int level)
		{
			if (level < 0 || level > 100)
			{
				Logger.Error("QscDspTcp {0} - SetAudioInputLevel({0}, {1}) - 'level' must be between 0 and 100.", id, level);
				return;
			}

			this.SetChannelLevel(id, level, this.inputs);
		}

		/// <inheritdoc/>
		public void SetAudioInputMute(string id, bool mute)
		{
			this.SetChannelMute(id, mute, this.inputs);
		}

		/// <inheritdoc/>
		public void SetAudioOutputLevel(string id, int level)
		{
			if (level < 0 || level > 100)
			{
				Logger.Error("QscDspTcp {0} - SetAudioOutputLevel({0}, {1}) - 'level' must be between 0 and 100.", id, level);
				return;
			}

			this.SetChannelLevel(id, level, this.outputs);
		}

		/// <inheritdoc/>
		public void SetAudioOutputMute(string id, bool mute)
		{
			this.SetChannelMute(id, mute, this.outputs);
		}

		/// <inheritdoc/>
		public string GetCurrentAudioSource(string outputId)
		{
			var found = this.outputs.FirstOrDefault(x => x.Id.Equals(outputId, StringComparison.InvariantCulture));
			if (found == null)
			{
				return string.Empty;
			}

			var source = this.inputs.Find(src => src.RouterIndex == found.AudioSource);
			if (source == null)
			{
				return string.Empty;
			}

			return source.Id;
		}

		/// <inheritdoc/>
		public void RouteAudio(string sourceId, string outputId)
		{
			var found = this.outputs.FirstOrDefault(x => x.Id.Equals(outputId, StringComparison.InvariantCulture));
			if (found == null)
			{
				Logger.Error("QscDsp {0} - No output channel found with ID {1}", this.Id, outputId);
				return;
			}

			var source = this.inputs.Find(src => src.Id.Equals(sourceId, StringComparison.InvariantCulture));
			if (source == null)
			{
				Logger.Error("QscDsp {0} - No input channel found with ID {1}", this.Id, sourceId);
				return;
			}

			found.SetAudioRoute((uint)source.RouterIndex);
		}

		/// <inheritdoc/>
		public void ClearAudioRoute(string outputId) { }

		/// <inheritdoc/>
		public void AddAudioZoneEnable(string channelId, string zoneId, string controlTag)
		{
			if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(zoneId) || string.IsNullOrEmpty(controlTag))
			{
				Logger.Error("QscDspTcp {0} - AddAudioZoneEnable() - no argument can be null or empty.", this.Id);
				return;
			}

			var channel = this.TryFindChannel(channelId, "AddAudioZoneEnable");
			channel?.AddZoneEnable(zoneId, controlTag);
		}

		/// <inheritdoc/>
		public void RemoveAudioZoneEnable(string channelId, string zoneId)
		{
			if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(zoneId))
			{
				Logger.Error("QscDspTcp {0} - RemoveAudioZoneEnable() - no argument can be null or empty.", this.Id);
				return;
			}

			var channel = this.TryFindChannel(channelId, "RemoveAudioZoneEnable");
			channel?.RemoveZoneEnable(zoneId);
		}

		/// <inheritdoc/>
		public void ToggleAudioZoneEnable(string channelId, string zoneId)
		{
			if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(zoneId))
			{
				Logger.Error("QscDspTcp {0} - ToggleAudioZoneEnable() - no argument can be null or empty.", this.Id);
				return;
			}

			var channel = this.TryFindChannel(channelId, "ToggleAudioZoneEnable");
			channel?.ToggleZoneEnableState(zoneId);
		}

		/// <inheritdoc/>
		public bool QueryAudioZoneEnable(string channelId, string zoneId)
		{
			if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(zoneId))
			{
				Logger.Error("QscDspTcp {0} - QueryAudioZoneEnable() - no argument can be null or empty.", this.Id);
				return false;
			}

			var channel = this.TryFindChannel(channelId, "QueryAudioZoneEnable");
			if (channel == null)
			{
				return false;
			}

			return channel.QueryZoneEnableState(zoneId);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			this.Dispose(true);
		}

		private void SetChannelLevel(string id, int level, List<QscAudioChannel> channels)
		{
			int percentMax = 100;
			int crestronMax = 65534;
			int scaled = (level * crestronMax) / percentMax;
			var found = channels.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found == null)
			{
				return;
			}

			found.SetAudioLevel(scaled);
		}

		private void SetChannelMute(string id, bool mute, List<QscAudioChannel> channels)
		{
			var found = channels.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found == null)
			{
				return;
			}

			found.SetAudioMute(mute);
		}

		private int ConvertLevelToPercent(int level)
		{
			int currentMax = 65534;
			int scaled = (level * 100) / currentMax;
			return scaled;
		}

		private void OnCoreConnected(SimplSharpString id, ushort state)
		{
			Logger.Debug("QscDspTcp {0}.OnCoreConnected() - {1}", this.Id, state);

			if (state > 0)
			{
				this.RegisternamedControls();
				this.IsOnline = true;
				this.NotifyOnlineStatus();
			}
			else
			{
				this.IsOnline = false;
				this.NotifyOnlineStatus();
			}
		}

		private void OnCoreStatusChange(SimplSharpString name, SimplSharpString designName, ushort redundant, ushort emulator)
		{
			Logger.Debug("QscDspTcp {0} - OnCoreStatusChange({1}, {2}, {3})", this.Id, name, redundant, emulator);
		}

		private void OnCoreRegistered(SimplSharpString id, ushort value)
		{
			Logger.Debug("QscDspTcp {0} - OnCoreRegistered({1})", this.Id, value);

			this.coreRegistered = value > 0;
			this.IsOnline = this.coreRegistered;
			this.NotifyOnlineStatus();
		}

		private void RegisternamedControls()
		{
			foreach (var output in this.outputs)
			{
				output.Register();
			}

			foreach (var input in this.inputs)
			{
				input.Register();
			}

			foreach (var snapshot in this.snapshots)
			{
				snapshot.Register();
			}
		}

		private void ClearControls()
		{
			this.core.onIsConnected -= this.OnCoreConnected;
			this.core.onNewCoreStatus -= this.OnCoreStatusChange;
			this.core.onIsRegistered -= this.OnCoreRegistered;

			foreach (var chan in this.inputs)
			{
				chan.AudioMuteChanged -= this.InputControlMuteChange;
				chan.AudioVolumeChanged -= this.InputControlLevelChange;
			}

			foreach (var chan in this.outputs)
			{
				chan.AudioMuteChanged -= this.OutputControlLevelChange;
				chan.AudioVolumeChanged -= this.OutputControlMuteChange;
				chan.AudioRouteChanged -= this.OutputControlRouteChanged;
			}

			this.inputs.Clear();
			this.outputs.Clear();
			this.snapshots.Clear();
		}

		private void InputControlLevelChange(object sender, GenericDualEventArgs<string, int> e)
		{
			var temp = this.AudioInputLevelChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, e.Arg1));
		}

		private void InputControlMuteChange(object sender, GenericDualEventArgs<string, int> e)
		{
			var temp = this.AudioInputMuteChanged;
			temp?.Invoke(sender, new GenericDualEventArgs<string, string>(this.Id, e.Arg1));
		}

		private void OutputControlLevelChange(object sender, GenericDualEventArgs<string, int> e)
		{
			Logger.Debug("QscDspTcp {0} - OutputControlLevelChange() - {1}, {2}", this.Id, e.Arg1, e.Arg2);

			var temp = this.AudioOutputLevelChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, e.Arg1));
		}

		private void OutputControlMuteChange(object sender, GenericDualEventArgs<string, int> e)
		{
			Logger.Debug("QscDspTcp {0} - OutputControlMuteChange() - {1}, {2}", this.Id, e.Arg1, e.Arg2);

			var temp = this.AudioOutputMuteChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, e.Arg1));
		}

		private void OutputControlRouteChanged(object sender, GenericDualEventArgs<string, uint> e)
		{
			Logger.Debug("QscDspTcp {0} - OutputControlRouteChanged() - {1}, {2}", this.Id, e.Arg1, e.Arg2);

			var output = this.outputs.FirstOrDefault(x => x.Id.Equals(e.Arg1, StringComparison.InvariantCulture));
			if (output == null)
			{
				return;
			}

			var temp = this.AudioRouteChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, output.Id));
		}

		private void ZoneEnableChangedHandler(object sender, GenericDualEventArgs<string, string> e)
		{
			var temp = this.AudioZoneEnableChanged;
			temp?.Invoke(this, e);
		}

		private QscAudioChannel TryFindChannel(string channelId, string callingMethodName)
		{
			var channel = this.inputs.FirstOrDefault(x => x.Id.Equals(channelId, StringComparison.InvariantCulture)) ?? this.outputs.FirstOrDefault(x => x.Id.Equals(channelId, StringComparison.InvariantCulture));
			if (channel == null)
			{
				Logger.Error("QscDspTcp {0} - {1}() - no input or output channel found with ID {2}", this.Id, callingMethodName, channelId);
			}

			return channel;
		}

		private void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				if (disposing)
				{
					this.ClearControls();
					if (this.core != null)
					{
						this.core.Dispose();
						this.core = null;
					}
				}

				this.disposed = true;
			}
		}
	}
}
