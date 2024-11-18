namespace NvxAvSwitch.DataObjects
{
	public class NvxTagData
	{
		public NvxTagData()
		{
			this.NvxId = string.Empty;
			this.ClassName = string.Empty;
			this.IpId = 0;
			this.InputPort = 0;
			this.OutputPort = 0;
		}

		public string NvxId { get; set; }
		public string ClassName { get; set; }
		public uint IpId { get; set; }
		public uint InputPort { get; set; }
		public uint OutputPort { get; set; }

		public override string ToString()
		{
			return string.Format(
				"ID: {0}, Class: {1}, IP-ID: 0x{2:X2}, InputPort: {3}, OutputPort: {4}",
				this.NvxId,
				this.ClassName,
				this.IpId,
				this.InputPort,
				this.OutputPort);
		}
	}
}
