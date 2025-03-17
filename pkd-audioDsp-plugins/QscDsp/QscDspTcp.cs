using pkd_hardware_service.Redundancy;

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
	/// Uses QscQsys dependency library for tx/rx, developed by Mat Klucznyk:
	/// https://github.com/MatKlucznyk/Qsys.
	/// </summary>
	public class QscDspTcp : BaseDevice, IDsp, IAudioRoutable, IAudioZoneEnabler, IRedundancySupport
	{
		private const int CrestronMax = 65534;
		private const int PercentMax = 100;
		private bool _disposed;
		private readonly List<QscAudioChannel> _inputs;
		private readonly List<QscAudioChannel> _outputs;
		private readonly List<QscSnapshotBank> _snapshots;
		private bool _coreRegistered;
		private string? _hostname;
		private string _backupHostname = string.Empty;
		private string? _username;
		private string? _password;
		private int _port;
		private QsysCore? _core;

		/// <summary>
		/// Initializes a new instance of the <see cref="QscDsp"/> class.
		/// </summary>
		public QscDspTcp()
		{
			Id = "QscDspDefaultID";
			Label = "QSC DSP";
			_inputs = [];
			_outputs = [];
			_snapshots = [];

			Manufacturer = "Q-Sys";
			Model = "QSC DSP";
		}

		~QscDspTcp()
		{
			Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputLevelChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioInputMuteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputLevelChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioOutputMuteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioRouteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AudioZoneEnableChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>>? RedundancyStateChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>>? BackupDeviceConnectionChanged;
		
		/// <inheritdoc/>
		public bool PrimaryDeviceActive { get; private set; }
		
		/// <inheritdoc/>
		public bool BackupDeviceActive { get; private set; }
		
		/// <inheritdoc/>
		public bool BackupDeviceOnline { get; private set; }

		/// <inheritdoc/>
		public void SetBackupDeviceConnection(string hostname, int port)
		{
			ParameterValidator.ThrowIfNullOrEmpty(hostname, nameof(SetBackupDeviceConnection),nameof(hostname));
			if (port is < 0 or > 65535)
			{
				throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 0 and 65535");
			}
			
			_backupHostname = hostname;
		}
		
		/// <inheritdoc/>
		public void AddInputChannel(string id, string levelTag, string muteTag, int bankIndex, int levelMax, int levelMin, int routerIndex)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "QscDspTcp.AddInputChannel", nameof(id));
			ParameterValidator.ThrowIfNullOrEmpty(levelTag, "QscDspTcp.AddInputChannel", nameof(levelTag));
			ParameterValidator.ThrowIfNullOrEmpty(muteTag, "QscDspTcp.AddInputChannel", nameof(muteTag));
			if (bankIndex < 0)
			{
				throw new ArgumentException("QscDspTcp.AddInputChannel() - 'bankIndex' cannot be less than 0.");
			}

			var existing = _inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (existing != null)
			{
				existing.AudioMuteChanged -= InputControlMuteChange;
				existing.AudioVolumeChanged -= InputControlLevelChange;
				_inputs.Remove(existing);
			}

			var channel = new QscAudioChannel(Id, id, muteTag, levelTag, [], routerIndex);
			channel.AudioMuteChanged += InputControlMuteChange;
			channel.AudioVolumeChanged += InputControlLevelChange;
			channel.AudioZoneEnableChanged += ZoneEnableChangedHandler;
			_inputs.Add(channel);
		}

		/// <inheritdoc/>
		public void AddOutputChannel(string id, string levelTag, string muteTag, string routerTag, int routerIndex, int bankIndex, int levelMax, int levelMin)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "QscDspTcp.AddOutputChannel", nameof(id));
			ParameterValidator.ThrowIfNullOrEmpty(levelTag, "QscDspTcp.AddOutputChannel", nameof(levelTag));
			ParameterValidator.ThrowIfNullOrEmpty(muteTag, "QscDspTcp.AddOutputChannel", nameof(muteTag));
			if (bankIndex < 0)
			{
				throw new ArgumentException("QscDspTcp.AddOutputChannel() - 'bankIndex' cannot be less than 0.");
			}


			var existing = _outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (existing != null)
			{
				existing.AudioVolumeChanged -= OutputControlLevelChange;
				existing.AudioMuteChanged -= OutputControlMuteChange;
				_outputs.Remove(existing);
			}

			var channel = new QscAudioChannel(Id, id, muteTag, levelTag, routerTag, [], routerIndex);
			channel.AudioMuteChanged += OutputControlMuteChange;
			channel.AudioVolumeChanged += OutputControlLevelChange;
			channel.AudioRouteChanged += OutputControlRouteChanged;
			channel.AudioZoneEnableChanged += ZoneEnableChangedHandler;
			_outputs.Add(channel);
		}

		/// <inheritdoc/>
		public void AddPreset(string id, int index)
		{
			ParameterValidator.ThrowIfNullOrEmpty(id, "QscDspTcp.AddPreset", nameof(id));
			if (index < 0)
			{
				throw new ArgumentException("QscDspTcp.AddPreset() - 'index' cannot be less than zero.");
			}

			var existing = _snapshots.Find(x => x.Name.Equals(id, StringComparison.InvariantCulture));
			if (existing == null)
			{
				existing = new QscSnapshotBank(Id, id);
				_snapshots.Add(existing);
			}

			var presetId = $"{id}.{index}";
			existing.AddPreset(presetId, index);
			if (!existing.IsRegistered)
			{
				existing.Register();
			}
		}

		/// <inheritdoc/>
		public void Initialize(string hostId, int coreId, string hostname, int port, string username, string password)
		{
			ParameterValidator.ThrowIfNullOrEmpty(hostId, "Initialize", nameof(coreId));
			ParameterValidator.ThrowIfNullOrEmpty(hostname, "Initialize", nameof(hostname));
			ParameterValidator.ThrowIfNull(username, "Initialize", nameof(username));
			ParameterValidator.ThrowIfNull(password, "Initialize", nameof(password));

			IsInitialized = false;
			Id = hostId;
			_hostname = hostname;
			_port = port;
			_username = username;
			_password = password;
			IsInitialized = true;
		}

		/// <inheritdoc/>
		public override void Connect()
		{
			if (IsInitialized)
			{
				_core = new QsysCore();

#if DEBUG
				_core.Debug(1);
#endif
				_core.OnPrimaryIsConnected = OnCoreConnected;
				_core.OnNewCoreStatus += OnCoreStatusChange;
				_core.OnIsRegistered += OnCoreRegistered;
				_core.OnBackupIsConnected += OnBackupConnected;
				_core.OnPrimaryIsActive += OnPrimaryActive;

				_core.Initialize(
					Id,
					_hostname ?? string.Empty,
					_backupHostname,
					(ushort)_port,
					_username ?? string.Empty,
					_password ?? string.Empty,
					0);
			}
			else
			{
				Logger.Error("QscDspTcp {0} - Cannot connect. Run Initialize() first.", Id);
			}
		}

		private void OnPrimaryActive()
		{
			PrimaryDeviceActive = _core?.PrimaryCoreActive ?? false;
			BackupDeviceActive = !PrimaryDeviceActive;
			var temp = RedundancyStateChanged;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
		}

		private void OnBackupConnected(SimplSharpString id, ushort value)
		{
			BackupDeviceOnline = value > 0;
			var temp = BackupDeviceConnectionChanged;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
		}

		/// <inheritdoc/>
		public override void Disconnect()
		{
			if (!IsInitialized) return;
			_core?.Dispose();
			_core = null;
			_coreRegistered = false;
			NotifyOnlineStatus();
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioInputIds()
		{
			return _inputs.Select(x => x.Id).ToList();
		}

		/// <inheritdoc/>
		public int GetAudioInputLevel(string id)
		{
			var found = _inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			if (found == null)
			{
				return 0;
			}

			return ConvertLevelToPercent(found.AudioLevel);
		}

		/// <inheritdoc/>
		public bool GetAudioInputMute(string id)
		{
			var found = _inputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			return found is { AudioMute: true };
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioOutputIds()
		{
			return _outputs.Select(x => x.Id).ToList();
		}

		/// <inheritdoc/>
		public int GetAudioOutputLevel(string id)
		{
			var found = _outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			return found == null ? 0 : ConvertLevelToPercent(found.AudioLevel);
		}

		/// <inheritdoc/>
		public bool GetAudioOutputMute(string id)
		{
			var found = _outputs.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			return found is { AudioMute: true };
		}

		/// <inheritdoc/>
		public IEnumerable<string> GetAudioPresetIds()
		{
			List<string> presetIds = [];
			foreach (var bank in _snapshots)
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
			foreach (var bank in _snapshots)
			{
				Logger.Debug("QscDspTcp.RecallAudioPreset({0})", id);
				
				if (!bank.HasPreset(id)) continue;
				bank.RecallPreset(id);
				break;
			}
		}

		/// <inheritdoc/>
		public void SetAudioInputLevel(string id, int level)
		{
			if (level is < 0 or > 100)
			{
				Logger.Error("QscDspTcp {0} - SetAudioInputLevel({0}, {1}) - 'level' must be between 0 and 100.", id, level);
				return;
			}

			SetChannelLevel(id, level, _inputs);
		}

		/// <inheritdoc/>
		public void SetAudioInputMute(string id, bool mute)
		{
			SetChannelMute(id, mute, _inputs);
		}

		/// <inheritdoc/>
		public void SetAudioOutputLevel(string id, int level)
		{
			if (level is < 0 or > 100)
			{
				Logger.Error("QscDspTcp {0} - SetAudioOutputLevel({0}, {1}) - 'level' must be between 0 and 100.", id, level);
				return;
			}

			SetChannelLevel(id, level, _outputs);
		}

		/// <inheritdoc/>
		public void SetAudioOutputMute(string id, bool mute)
		{
			SetChannelMute(id, mute, _outputs);
		}

		/// <inheritdoc/>
		public string GetCurrentAudioSource(string outputId)
		{
			var found = _outputs.FirstOrDefault(x => x.Id.Equals(outputId, StringComparison.InvariantCulture));
			if (found == null)
			{
				return string.Empty;
			}

			var source = _inputs.Find(src => src.RouterIndex == found.AudioSource);
			return source == null ? string.Empty : source.Id;
		}

		/// <inheritdoc/>
		public void RouteAudio(string sourceId, string outputId)
		{
			var found = _outputs.FirstOrDefault(x => x.Id.Equals(outputId, StringComparison.InvariantCulture));
			if (found == null)
			{
				Logger.Error("QscDsp {0} - No output channel found with ID {1}", Id, outputId);
				return;
			}

			var source = _inputs.Find(src => src.Id.Equals(sourceId, StringComparison.InvariantCulture));
			if (source == null)
			{
				Logger.Error("QscDsp {0} - No input channel found with ID {1}", Id, sourceId);
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
				Logger.Error("QscDspTcp {0} - AddAudioZoneEnable() - no argument can be null or empty.", Id);
				return;
			}

			var channel = TryFindChannel(channelId, "AddAudioZoneEnable");
			channel?.AddZoneEnable(zoneId, controlTag);
		}

		/// <inheritdoc/>
		public void RemoveAudioZoneEnable(string channelId, string zoneId)
		{
			if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(zoneId))
			{
				Logger.Error("QscDspTcp {0} - RemoveAudioZoneEnable() - no argument can be null or empty.", Id);
				return;
			}

			var channel = TryFindChannel(channelId, "RemoveAudioZoneEnable");
			channel?.RemoveZoneEnable(zoneId);
		}

		/// <inheritdoc/>
		public void ToggleAudioZoneEnable(string channelId, string zoneId)
		{
			if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(zoneId))
			{
				Logger.Error("QscDspTcp {0} - ToggleAudioZoneEnable() - no argument can be null or empty.", Id);
				return;
			}

			var channel = TryFindChannel(channelId, "ToggleAudioZoneEnable");
			channel?.ToggleZoneEnableState(zoneId);
		}

		/// <inheritdoc/>
		public bool QueryAudioZoneEnable(string channelId, string zoneId)
		{
			if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(zoneId))
			{
				Logger.Error("QscDspTcp {0} - QueryAudioZoneEnable() - no argument can be null or empty.", Id);
				return false;
			}

			var channel = TryFindChannel(channelId, "QueryAudioZoneEnable");
			if (channel == null)
			{
				return false;
			}

			return channel.QueryZoneEnableState(zoneId);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Dispose(true);
		}

		private static void SetChannelLevel(string id, int level, List<QscAudioChannel> channels)
		{
			var scaled = (level * CrestronMax) / PercentMax;
			var found = channels.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			found?.SetAudioLevel(scaled);
		}

		private static void SetChannelMute(string id, bool mute, List<QscAudioChannel> channels)
		{
			var found = channels.Find(x => x.Id.Equals(id, StringComparison.InvariantCulture));
			found?.SetAudioMute(mute);
		}

		private static int ConvertLevelToPercent(int level)
		{
			var scaled = (level * PercentMax) / CrestronMax;
			return scaled;
		}

		private void OnCoreConnected(SimplSharpString id, ushort state)
		{
			Logger.Debug("QscDspTcp {0}.OnCoreConnected() - {1}", Id, state);

			if (state > 0)
			{
				RegisterNamedControls();
				IsOnline = true;
			}
			else
			{
				IsOnline = false;
			}
			
			NotifyOnlineStatus();
		}

		private void OnCoreStatusChange(SimplSharpString name, SimplSharpString designName, ushort redundant, ushort emulator)
		{
			Logger.Debug("QscDspTcp {0} - OnCoreStatusChange({1}, {2}, {3})", Id, name, redundant, emulator);
		}

		private void OnCoreRegistered(SimplSharpString id, ushort value)
		{
			Logger.Debug("QscDspTcp {0} - OnCoreRegistered({1})", Id, value);

			_coreRegistered = value > 0;
			IsOnline = _coreRegistered;
			NotifyOnlineStatus();
		}

		private void RegisterNamedControls()
		{
			foreach (var output in _outputs)
			{
				output.Register();
			}

			foreach (var input in _inputs)
			{
				input.Register();
			}

			foreach (var snapshot in _snapshots)
			{
				snapshot.Register();
			}
		}

		private void ClearControls()
		{
			if (_core == null) return;
			_core.OnPrimaryIsConnected -= OnCoreConnected;
			_core.OnNewCoreStatus -= OnCoreStatusChange;
			_core.OnIsRegistered -= OnCoreRegistered;

			foreach (var chan in _inputs)
			{
				chan.AudioMuteChanged -= InputControlMuteChange;
				chan.AudioVolumeChanged -= InputControlLevelChange;
			}

			foreach (var chan in _outputs)
			{
				chan.AudioMuteChanged -= OutputControlLevelChange;
				chan.AudioVolumeChanged -= OutputControlMuteChange;
				chan.AudioRouteChanged -= OutputControlRouteChanged;
			}

			_inputs.Clear();
			_outputs.Clear();
			_snapshots.Clear();
		}

		private void InputControlLevelChange(object? sender, GenericDualEventArgs<string, int> e)
		{
			var temp = AudioInputLevelChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(Id, e.Arg1));
		}

		private void InputControlMuteChange(object? sender, GenericDualEventArgs<string, int> e)
		{
			var temp = AudioInputMuteChanged;
			temp?.Invoke(sender, new GenericDualEventArgs<string, string>(Id, e.Arg1));
		}

		private void OutputControlLevelChange(object? sender, GenericDualEventArgs<string, int> e)
		{
			Logger.Debug("QscDspTcp {0} - OutputControlLevelChange() - {1}, {2}", Id, e.Arg1, e.Arg2);

			var temp = AudioOutputLevelChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(Id, e.Arg1));
		}

		private void OutputControlMuteChange(object? sender, GenericDualEventArgs<string, int> e)
		{
			Logger.Debug("QscDspTcp {0} - OutputControlMuteChange() - {1}, {2}", Id, e.Arg1, e.Arg2);

			var temp = AudioOutputMuteChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(Id, e.Arg1));
		}

		private void OutputControlRouteChanged(object? sender, GenericDualEventArgs<string, uint> e)
		{
			Logger.Debug("QscDspTcp {0} - OutputControlRouteChanged() - {1}, {2}", Id, e.Arg1, e.Arg2);

			var output = _outputs.FirstOrDefault(x => x.Id.Equals(e.Arg1, StringComparison.InvariantCulture));
			if (output == null)
			{
				return;
			}

			var temp = AudioRouteChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(Id, output.Id));
		}

		private void ZoneEnableChangedHandler(object? sender, GenericDualEventArgs<string, string> e)
		{
			var temp = AudioZoneEnableChanged;
			temp?.Invoke(this, e);
		}

		private QscAudioChannel? TryFindChannel(string channelId, string callingMethodName)
		{
			var channel = _inputs.FirstOrDefault(x => x.Id.Equals(channelId, StringComparison.InvariantCulture)) ?? _outputs.FirstOrDefault(x => x.Id.Equals(channelId, StringComparison.InvariantCulture));
			if (channel == null)
			{
				Logger.Error("QscDspTcp {0} - {1}() - no input or output channel found with ID {2}", Id, callingMethodName, channelId);
			}

			return channel;
		}

		private void Dispose(bool disposing)
		{
			if (_disposed) return;
			if (disposing)
			{
				ClearControls();
				if (_core != null)
				{
					_core.Dispose();
					_core = null;
				}
			}

			_disposed = true;
		}
	}
}
