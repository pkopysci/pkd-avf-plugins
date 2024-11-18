namespace NvxAvSwitch.DataObjects
{
	internal class NvxSource
	{
		public NvxSource()
		{
			this.Id = string.Empty;
			this.RouterIndex = 0;
			this.NvxInputPort = 0;
		}

		public string Id { get; set; }
		public uint RouterIndex { get; set; }
		public uint NvxInputPort { get; set; }
		public NvxIpEndpoint Endpoint { get; set; }

		public override string ToString()
		{
			return string.Format(
				"NvxSource - ID: {0}, RouterIndex: {1}, NvxInputPort: {2}, Endpoint: {3}",
				this.Id,
				this.RouterIndex,
				this.NvxInputPort,
				this.Endpoint);
		}
	}
}
