using QscQsys.NamedComponents;

namespace QscDsp
{
	using Crestron.SimplSharp;
	using pkd_common_utils.Logging;
	using pkd_common_utils.Validation;
	using QscQsys;
	using System.Collections.Generic;

	/// <summary>
	/// Wrapper class for tracking and controlling individual QSC design snapshot banks.
	/// </summary>
	internal class QscSnapshotBank
	{
		private readonly string _coreId;
		private readonly QsysSnapshot _snapshot;
		private readonly Dictionary<string, int> _presets;

		/// <summary>
		/// Initializes a new instance of the <see cref="QscSnapshotBank"/> class.
		/// </summary>
		/// <param name="coreId">The unique ID of the QSYS Core that will be controlled.</param>
		/// <param name="bankName">The named snapshot bank that is set in the core design.</param>
		public QscSnapshotBank(string coreId, string bankName)
		{
			ParameterValidator.ThrowIfNullOrEmpty(coreId, "QscSnapshotBank.Ctor", nameof(coreId));
			ParameterValidator.ThrowIfNullOrEmpty(bankName, "QscSnapshotBank.Ctor", nameof(bankName));
			Name = bankName;
			_coreId = coreId;
			_presets = new Dictionary<string, int>();
			_snapshot = new QsysSnapshot();
		}

		/// <summary>
		/// Gets the user-friendly name of this snapshot bank.
		/// </summary>
		public string Name { get; private set; }
		
		/// <summary>
		/// Gets a value indicating whether the snapshot bank has been registered with the control object.
		/// </summary>
		public bool IsRegistered { get; private set; }

		/// <summary>
		/// Gets the Ids of all presets stored in the snapshot.
		/// </summary>
		public IEnumerable<string> PresetIds => _presets.Keys;

		/// <summary>
		/// Check the collection of added presets for the target ID.
		/// </summary>
		/// <param name="presetId">the unique ID of the preset to check for.</param>
		/// <returns>True if the preset was found, false otherwise.</returns>
		public bool HasPreset(string presetId)
		{
			return _presets.ContainsKey(presetId);
		}

		/// <summary>
		/// Add a preset to the collection. If a preset currently exists with the
		/// same ID it will be replaced.
		/// </summary>
		/// <param name="presetId">The unique id of the preset being added.</param>
		/// <param name="presetIndex">The bank index of the preset being added.</param>
		public void AddPreset(string presetId, int presetIndex)
		{
			ParameterValidator.ThrowIfNullOrEmpty(presetId, "QscSnapshotBank.AddPreset", nameof(presetId));
			if (!_presets.TryAdd(presetId, presetIndex))
			{
				_presets[presetId] = presetIndex;
			}
        }

        /// <summary>
        /// Attempts to load a bank at the given index.
        /// Writes a warning to the system logger if the bank cannot be found.
        /// </summary>
        /// <param name="presetId">The unique ID of the snapshot bank to recall.</param>
        /// <returns>True if the recall was successful, false otherwise.</returns>
        public bool RecallPreset(string presetId)
		{
			if (_presets.TryGetValue(presetId, out int found))
			{
				_snapshot.LoadSnapshot((ushort)found);
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Initializes the internal communication objects and registers them with the core.
		/// </summary>
		public void Register()
		{
			Logger.Debug("Registering Snapshot bank {0} with core {1}", Name, _coreId);
			_snapshot.onRecalledSnapshot = SnapshotRecalled;
			_snapshot.Initialize(_coreId, Name, 8);
			IsRegistered = true;
		}


		private void SnapshotRecalled(SimplSharpString name, ushort index)
		{
			Logger.Debug("QscSnapshotBank {0} - SnapshotRecalled({1})", Name, index);
		}
	}
}
