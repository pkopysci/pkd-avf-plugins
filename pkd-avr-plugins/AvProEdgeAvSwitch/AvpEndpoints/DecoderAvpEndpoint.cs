namespace AvProEdgeAvSwitch.AvpEndpoints
{
	using AVProEdgeMXNetLib.Components;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using System;

	internal class DecoderAvpEndpoint : AvpEndpoint
	{
		private readonly DecoderComponent decoder;
		private readonly DestinationRouterComponent router;
		private bool decoderOnline;
		private bool routerInitialized;

		public DecoderAvpEndpoint(string macAddress, ushort processorId, ushort index)
			: base(macAddress, processorId, index)
		{
			this.decoder = new DecoderComponent();
			this.router = new DestinationRouterComponent();
		}

		public event EventHandler<GenericDualEventArgs<string, uint>> VideoRouteChanged;

		public event EventHandler<GenericDualEventArgs<string, uint>> AudioRouteChanged;

		public override bool IsOnline { get { return this.decoderOnline; } }

		public override bool IsInitialized { get { return this.routerInitialized; } }

		public uint CurrentVideoSource { get; private set; }

		public uint CurrentAudioSource { get; private set; }

		public void RouteSource(uint input)
		{
			if (!this.IsOnline || !this.IsInitialized)
			{
				Logger.Error(
					"AvProEdge decoder {0} - RouteSource() - Invalid state. online =  {1}, init = {2}",
					this.MacAddress,
					this.IsOnline,
					this.IsInitialized);

				return;
			}

			this.router.SetRoute((ushort)input);
			this.router.TakeRoute();
		}

		public override void Initialize()
		{
			Logger.Debug("Initializing DecoderAvpEndpoint {0} with device {1}", this.MacAddress, this.ProcessorId);

			this.decoder.OnOnline += new AVProEdgeMXNetLib.EventArguments.DigEventHandler(Decoder_OnOnline);
			this.decoder.OnInitialize += new AVProEdgeMXNetLib.EventArguments.DigEventHandler(Decoder_OnInitialize);

			this.router.OnInitialize += new AVProEdgeMXNetLib.EventArguments.DigEventHandler(Router_OnInitialize);
			this.router.OnSourceAudio += new AVProEdgeMXNetLib.EventArguments.AnaEventHandler(Router_OnSourceAudio);
			this.router.OnSourceVideo += new AVProEdgeMXNetLib.EventArguments.AnaEventHandler(Router_OnSourceVideo);
			this.router.SetEnableVideo(1);

			this.decoder.Configure(this.ProcessorId, this.MacAddress, this.Index);
			this.router.Configure(this.ProcessorId, this.Index, string.Empty);
		}

		private void Decoder_OnInitialize(object sender, AVProEdgeMXNetLib.EventArguments.DigEventArgs args)
		{
			this.NotifyInitializeChanged();
		}

		private void Decoder_OnOnline(object sender, AVProEdgeMXNetLib.EventArguments.DigEventArgs args)
		{
			this.decoderOnline = args.Payload > 0;
			this.NotifyOnlineChanged();
		}

		private void Router_OnSourceVideo(object sender, AVProEdgeMXNetLib.EventArguments.AnaEventArgs args)
		{
			this.CurrentVideoSource = (uint)args.Payload;
			this.Notify(this.VideoRouteChanged, this.Index);
		}

		private void Router_OnSourceAudio(object sender, AVProEdgeMXNetLib.EventArguments.AnaEventArgs args)
		{
			this.CurrentAudioSource = (uint)args.Payload;
			this.Notify(this.AudioRouteChanged, this.Index);
		}

		private void Router_OnInitialize(object sender, AVProEdgeMXNetLib.EventArguments.DigEventArgs args)
		{
			this.routerInitialized = args.Payload > 0;
			this.NotifyInitializeChanged();
		}

		private void Notify(EventHandler<GenericDualEventArgs<string, uint>> handler, uint arg)
		{
			var temp = handler;
			temp?.Invoke(this, new GenericDualEventArgs<string, uint>(this.MacAddress, arg));
		}
	}
}
