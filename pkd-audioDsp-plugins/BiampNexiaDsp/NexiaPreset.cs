﻿namespace BiampNexiaDsp
{
	using pkd_common_utils.Validation;
	using System;

	internal class NexiaPreset
	{
		public NexiaPreset(int hostId, string presetId, int presetIndex)
		{
			ParameterValidator.ThrowIfNullOrEmpty(presetId, "Ctor", "presetId");

			this.HostId = hostId;
			this.Id = presetId;
			this.Index = presetIndex;
		}

		public int HostId { get; private set; }

		public string Id { get; private set; }

		public int Index { get; private set; }

		public Action<string> QueueCommand { get; set; }

		public void RecallPreset()
		{

			// RECALL 0 PRESET 1001 <LF>
			this.QueueCommand?.Invoke(string.Format(
					"{0} 0 {1} {2}\n",
					NexiaComander.Commands[NexiaCommands.Recall],
					NexiaComander.Blocks[NexiaBlocks.Preset],
					this.Index));
		}
	}
}