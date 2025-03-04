namespace AvProEdgeAvSwitch.AvpEndpoints;

using AVProEdgeMXNetLib.Components;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using System;

// TODO: Add audio routing support.
internal class DecoderAvpEndpoint(string macAddress, ushort processorId, ushort index)
	: AvpEndpoint(macAddress, processorId, index)
{
	private readonly DecoderComponent _decoder = new();
	private readonly DestinationRouterComponent _router = new();
	private bool _decoderOnline;
	private bool _routerInitialized;

	public event EventHandler<GenericDualEventArgs<string, uint>>? VideoRouteChanged;

	public override bool IsOnline => _decoderOnline;

	public override bool IsInitialized => _routerInitialized;

	public uint CurrentVideoSource { get; private set; }

	public void RouteSource(uint input)
	{
		if (!IsOnline || !IsInitialized)
		{
			Logger.Error(
				"AvProEdge decoder {0} - RouteSource() - Invalid state. online =  {1}, init = {2}",
				MacAddress,
				IsOnline,
				IsInitialized);

			return;
		}

		_router.SetRoute((ushort)input);
		_router.TakeRoute();
	}

	public override void Initialize()
	{
		Logger.Debug("Initializing DecoderAvpEndpoint {0} with device {1}", MacAddress, ProcessorId);

		_decoder.OnOnline += Decoder_OnOnline;
		_decoder.OnInitialize += Decoder_OnInitialize;

		_router.OnInitialize += Router_OnInitialize;
		_router.OnSourceVideo += Router_OnSourceVideo;
		_router.SetEnableVideo(1);

		_decoder.Configure(ProcessorId, MacAddress, Index);
		_router.Configure(ProcessorId, Index, string.Empty);
	}

	private void Decoder_OnInitialize(object sender, AVProEdgeMXNetLib.EventArguments.DigEventArgs args)
	{
		NotifyInitializeChanged();
	}

	private void Decoder_OnOnline(object sender, AVProEdgeMXNetLib.EventArguments.DigEventArgs args)
	{
		_decoderOnline = args.Payload > 0;
		NotifyOnlineChanged();
	}

	private void Router_OnSourceVideo(object sender, AVProEdgeMXNetLib.EventArguments.AnaEventArgs args)
	{
		CurrentVideoSource = args.Payload;
		Notify(VideoRouteChanged, Index);
	}

	private void Router_OnInitialize(object sender, AVProEdgeMXNetLib.EventArguments.DigEventArgs args)
	{
		_routerInitialized = args.Payload > 0;
		NotifyInitializeChanged();
	}

	private void Notify(EventHandler<GenericDualEventArgs<string, uint>>? handler, uint arg)
	{
		handler?.Invoke(this, new GenericDualEventArgs<string, uint>(MacAddress, arg));
	}
}