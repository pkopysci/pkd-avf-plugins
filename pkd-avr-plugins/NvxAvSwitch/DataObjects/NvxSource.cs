namespace NvxAvSwitch.DataObjects
{
	internal class NvxSource
	{
		public NvxSource()
		{
			Id = string.Empty;
			RouterIndex = 0;
			NvxInputPort = 0;
		}

		public string Id { get; init; } 
		public uint RouterIndex { get; init; }
		public uint NvxInputPort { get; init; }
		public NvxIpEndpoint? Endpoint { get; init; }

		public override string ToString()
		{
			return string.Format(
				"NvxSource - ID: {0}, RouterIndex: {1}, NvxInputPort: {2}, Endpoint: {3}",
				Id,
				RouterIndex,
				NvxInputPort,
				Endpoint);
		}
	}
}
