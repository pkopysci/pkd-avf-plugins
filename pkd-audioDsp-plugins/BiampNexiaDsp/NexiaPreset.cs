namespace BiampNexiaDsp
{
	using pkd_common_utils.Validation;
	using System;

	internal class NexiaPreset
	{
		public NexiaPreset(int hostId, string presetId, int presetIndex)
		{
			ParameterValidator.ThrowIfNullOrEmpty(presetId, "Ctor", "presetId");

			HostId = hostId;
			Id = presetId;
			Index = presetIndex;
		}

		public int HostId { get; private set; }

		public string Id { get; private set; }

		public int Index { get; private set; }

		public Action<string>? QueueCommand { get; set; }

		public void RecallPreset()
		{

			// RECALL 0 PRESET 1001 <LF>
			QueueCommand?.Invoke(string.Format(
					"{0} 0 {1} {2}\n",
					NexiaCommander.Commands[NexiaCommands.Recall],
					NexiaCommander.Blocks[NexiaBlocks.Preset],
					this.Index));
		}
	}
}
