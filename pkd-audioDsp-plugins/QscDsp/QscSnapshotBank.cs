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
		private readonly string coreId;
		private readonly QsysSnapshot snapshot;
		private readonly Dictionary<string, int> presets;

		/// <summary>
		/// Initializes a new instance of the <see cref="QscSnapshotBank"/> class.
		/// </summary>
		/// <param name="coreId">The unique ID of the QSYS Core that will be controlled.</param>
		/// <param name="bankName">The named snapshot bank that is set in the core design.</param>
		public QscSnapshotBank(string coreId, string bankName)
		{
			ParameterValidator.ThrowIfNullOrEmpty(coreId, "QscSnapshotBank.Ctor", "coreId");
			ParameterValidator.ThrowIfNullOrEmpty(bankName, "QscSnapshotBank.Ctor", "bankName");
			this.Name = bankName;
			this.coreId = coreId;
			this.presets = new Dictionary<string, int>();
			this.snapshot = new QsysSnapshot();
		}

		public string Name { get; private set; }
		public bool IsRegistered { get; private set; }

		/// <summary>
		/// Gets the Ids of all presets stored in the snapshot.
		/// </summary>
		public IEnumerable<string> PresetIds
		{
			get
			{
				return this.presets.Keys;
			}
		}

		/// <summary>
		/// Check the collection of added presets for the target ID.
		/// </summary>
		/// <param name="presetId">the unique ID of the preset to check for.</param>
		/// <returns>True if the preset was found, false otherwise.</returns>
		public bool HasPreset(string presetId)
		{
			return this.presets.ContainsKey(presetId);
		}

		/// <summary>
		/// Add a preset to the collection. If a preset currently exists with the
		/// same ID it will be replaced.
		/// </summary>
		/// <param name="data">The configuration data containing the preset index to control.</param>
		public void AddPreset(string presetId, int presetIndex)
		{
			ParameterValidator.ThrowIfNullOrEmpty(presetId, "QscSnapshotbank.AddPreset", "presetId");
			if (this.presets.ContainsKey(presetId))
			{
				this.presets[presetId] = presetIndex;
			}
			else
			{
				this.presets.Add(presetId, presetIndex);
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
			if (this.presets.TryGetValue(presetId, out int found))
			{
				this.snapshot.LoadSnapshot((ushort)found);
				return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Initializes the internal comunication objects and registers them with the core.
		/// </summary>
		public void Register()
		{
			Logger.Debug("Registering Snapshot bank {0} with core {1}", this.Name, this.coreId);
			this.snapshot.onRecalledSnapshot = this.SnapshotRecalled;
			this.snapshot.Initialize(this.coreId, this.Name);
			this.IsRegistered = true;
		}


		private void SnapshotRecalled(SimplSharpString name, ushort index)
		{
			Logger.Debug("QscSnapshotBank {0} - SnapshotRecalled({1})", this.Name, index);
		}
	}
}
