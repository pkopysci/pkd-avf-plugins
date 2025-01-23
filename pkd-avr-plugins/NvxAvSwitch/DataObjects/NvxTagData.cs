namespace NvxAvSwitch.DataObjects
{
	public class NvxTagData
	{
		public string NvxId { get; set; } = string.Empty;
		public string ClassName { get; set; } = string.Empty;
		public uint IpId { get; set; }
		public uint InputPort { get; set; }
		public uint OutputPort { get; set; }

		public override string ToString()
		{
			return string.Format(
				"ID: {0}, Class: {1}, IP-ID: 0x{2:X2}, InputPort: {3}, OutputPort: {4}",
				NvxId,
				ClassName,
				IpId,
				InputPort,
				OutputPort);
		}
	}
}
