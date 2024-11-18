namespace AvProEdgeAvSwitch.AvpEndpoints
{
	using AVProEdgeMXNetLib.Components;
	using pkd_common_utils.Logging;

	internal class EncoderAvpEndpoint : AvpEndpoint
	{
		private readonly EncoderComponent encoder;

		public EncoderAvpEndpoint(InputEncoder configObject, ushort processorId, ushort index)
			: base(configObject.MACADDRESSSAUTO, processorId, index)
		{
			this.encoder = new EncoderComponent();
			this.Label = string.Format("{0} - {1}", configObject.CUSTOMNAME, configObject.DESCRIPTION);
			this.EndpointType = pkd_hardware_service.AvIpMatrix.AvIpEndpointTypes.Encoder;
		}

		public override void Initialize()
		{
			Logger.Debug("Initializing EncoderAvpEndpoint {0} with device {1}", this.MacAddress, this.ProcessorId);

			this.encoder.OnOnline += new AVProEdgeMXNetLib.EventArguments.DigEventHandler(Encoder_OnOnline);
			this.encoder.OnInitialize += new AVProEdgeMXNetLib.EventArguments.DigEventHandler(Encoder_OnInitialize);
			this.encoder.Configure(this.ProcessorId, this.MacAddress, this.Index);
		}

		private void Encoder_OnOnline(object sender, AVProEdgeMXNetLib.EventArguments.DigEventArgs args)
		{
			this.IsOnline = args.Payload > 0;
			this.NotifyOnlineChanged();
		}

		private void Encoder_OnInitialize(object sender, AVProEdgeMXNetLib.EventArguments.DigEventArgs args)
		{
			this.IsInitialized = args.Payload > 0;
			this.NotifyInitializeChanged();
		}
	}
}
