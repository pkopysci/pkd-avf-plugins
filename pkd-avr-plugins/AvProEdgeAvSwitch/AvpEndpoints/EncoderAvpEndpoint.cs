namespace AvProEdgeAvSwitch.AvpEndpoints;

using AVProEdgeMXNetLib.Components;
using pkd_common_utils.Logging;

internal class EncoderAvpEndpoint : AvpEndpoint
{
	private readonly EncoderComponent _encoder;

	public EncoderAvpEndpoint(InputEncoder configObject, ushort processorId, ushort index)
		: base(configObject.MacAddressAuto, processorId, index)
	{
		_encoder = new EncoderComponent();
		EndpointType = pkd_hardware_service.AvIpMatrix.AvIpEndpointTypes.Encoder;
	}

	public override void Initialize()
	{
		Logger.Debug("Initializing EncoderAvpEndpoint {0} with device {1}", MacAddress, ProcessorId);

		_encoder.OnOnline += Encoder_OnOnline;
		_encoder.OnInitialize += Encoder_OnInitialize;
		_encoder.Configure(ProcessorId, MacAddress, Index);
	}

	private void Encoder_OnOnline(object sender, AVProEdgeMXNetLib.EventArguments.DigEventArgs args)
	{
		IsOnline = args.Payload > 0;
		NotifyOnlineChanged();
	}

	private void Encoder_OnInitialize(object sender, AVProEdgeMXNetLib.EventArguments.DigEventArgs args)
	{
		IsInitialized = args.Payload > 0;
		NotifyInitializeChanged();
	}
}