namespace AvProEdgeAvSwitch
{
	using AvProEdgeAvSwitch.AvpEndpoints;
	using AVProEdgeMXNetLib;
	using AVProEdgeMXNetLib.Components;
	using Crestron.SimplSharpPro;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_common_utils.Validation;
	using pkd_hardware_service.AvIpMatrix;
	using pkd_hardware_service.AvSwitchDevices;
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// GCU C# Framework plugin for controlling an AVProEdge MXNet C-box.
	/// </summary>
	public class AvProEdgeAvSwitchTcp : IAvSwitcher, IDisposable, IAvIpMatrix
	{
		private static readonly ushort POLL_TIME = 60;
		private static readonly ushort DEFAULT_ID = 1;
		private readonly CommandProcessor comProcessor;
		private readonly ControlBoxComponent controlBox;
		private readonly List<EncoderAvpEndpoint> encoders;
		private readonly List<DecoderAvpEndpoint> decoders;
		private bool commReady;
		private bool disposed;

		/// <summary>
		/// Instantiates a new instance of <see cref="AvProEdgeAvSwitchTcp"/>. Call Initialize() to connect and begin device control.
		/// </summary>
		public AvProEdgeAvSwitchTcp()
		{
			this.comProcessor = new CommandProcessor();
			this.controlBox = new ControlBoxComponent();
			this.encoders = new List<EncoderAvpEndpoint>();
			this.decoders = new List<DecoderAvpEndpoint>();
		}

		~AvProEdgeAvSwitchTcp()
		{
			this.Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> ConnectionChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, uint>> VideoRouteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AvIpEndpointStatusChanged;

		/// <inheritdoc/>
		public string Id { get; private set; }

		/// <inheritdoc/>
		public bool IsInitialized { get; private set; }

		/// <inheritdoc/>
		public bool IsOnline { get; private set; }

		/// <inheritdoc/>
		public string Label { get; private set; }

		/// <inheritdoc/>
		public void AddEndpoint(string id, List<string> tags, int ioIndex, AvIpEndpointTypes endpointType, CrestronControlSystem control) { }

		/// <inheritdoc/>
		public void Initialize(string hostName, int port, string id, string label, int numInputs, int numOutputs)
		{
			ParameterValidator.ThrowIfNullOrEmpty(hostName, "AvProEdgeAvSwitchTcp.Initialize", "hostName");
			ParameterValidator.ThrowIfNullOrEmpty(id, "AvProEdgeAvSwitchTcp.Initialize", "id");

			this.Id = id;
			this.Label = label;

			var config = MxnetConfigReader.TryReadConfig(this.Id);
			if (config == null)
			{
				Logger.Error("AvProEdgeAvSwitchTcp.Initialize() - Failed to retrieve router configuration. Cannot initialize system.");
				return;
			}

			this.CreateAndSubscribeEncoders(config);
			this.CreateAndSubscribeDecoders(config);

			this.comProcessor.OnReadyState += this.ComProcessorReadyChangeHandler;
			this.comProcessor.OnInitializationChange += this.ComProcessorInitChangeHandler;
			this.comProcessor.OnCommunicationChange += this.ComProcessorCommChangeHandler;
			this.comProcessor.Configure(DEFAULT_ID, hostName, POLL_TIME);

			this.controlBox.Configure(DEFAULT_ID);
			this.controlBox.ProcessIPAddress(hostName);
		}

		/// <inheritdoc/>
		public void Connect()
		{
			if (this.commReady)
			{
				this.comProcessor.Connect();
			}
		}

		/// <inheritdoc/>
		public void Disconnect()
		{
			if (this.commReady)
			{
				this.comProcessor.Disconnect();
			}
		}

		/// <inheritdoc/>
		public void ClearVideoRoute(uint output)
		{
			if (output > this.decoders.Count)
			{
				Logger.Error("AvProEdgeSwitchTcp.ClearVideoRoute({0}) - output exceeds max outputs.", output);
				return;
			}

			this.decoders[(int)output - 1].RouteSource(0);
		}

		/// <inheritdoc/>
		public uint GetCurrentVideoSource(uint output)
		{
			if (output > this.decoders.Count)
			{
				Logger.Error("AvProEdgeSwitchTcp.GetCurrentVideoSource({0}) - output exceeds max outputs.", output);
				return 0;
			}

			return this.decoders[(int)output - 1].CurrentVideoSource;
		}

		/// <inheritdoc/>
		public void RouteVideo(uint source, uint output)
		{
			Logger.Debug("AvProEdgeAvSwitchTcp.RouteVideo({0}, {1})", source, output);

			if (source > this.encoders.Count)
			{
				Logger.Error("AvProEdgeSwitchTcp.RouteVideo({0}, {1}) - source exceeds max inputs.", source, output);
				return;
			}

			if (output > this.decoders.Count)
			{
				Logger.Error("AvProEdgeSwitchTcp.RouteVideo({0}, {1}) - output exceeds max outputs.", source, output);
				return;
			}

			this.decoders[(int)output - 1].RouteSource(source);
		}

		/// <inheritdoc/>
		public IAvIpEndpoint GetAvIpEndpoint(string deviceId)
		{
			foreach (var encoder in this.encoders)
			{
				if (encoder.MacAddress.Equals(deviceId, StringComparison.InvariantCulture))
				{
					return encoder;
				}
			}

			foreach (var decoder in this.decoders)
			{
				if (decoder.MacAddress.Equals(deviceId, StringComparison.InvariantCulture))
				{
					return decoder;
				}
			}

			return null;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				if (disposing)
				{
					// TODO: dispose as necessary
				}

				this.disposed = true;
			}
		}

		private void CreateAndSubscribeEncoders(MxnetConfig config)
		{
			for (int i = 0; i < config.input_encoders.Count; i++)
			{
				var input = config.input_encoders[i];
				var encoder = new EncoderAvpEndpoint(input, DEFAULT_ID, (ushort)(i + 1));
				this.encoders.Add(encoder);

				encoder.InitializationChanged += (o, a) =>
				{
					Logger.Debug("AvProEdgeAvSwitchTcp - encoder {0} init status changed: {1}", a.Arg1, a.Arg2);
				};

				encoder.ConnectionStatusChanged += (o, a) =>
				{
					Logger.Debug("AvProEdgeAvSwitchTcp - encoder {0} online status changed: {1}", a.Arg1, a.Arg2);
					this.AvIpEndpointStatusChanged?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, a.Arg1));
				};

				encoder.Initialize();
			}
		}

		private void CreateAndSubscribeDecoders(MxnetConfig config)
		{
			for (int i = 0; i < config.outputs_decoders.Count; i++)
			{
				var output = config.outputs_decoders[i];
				var decoder = new DecoderAvpEndpoint(output.MACADDRESSSAUTO, DEFAULT_ID, (ushort)(i + 1));
				this.decoders.Add(decoder);

				decoder.ConnectionStatusChanged += (o, a) =>
				{
					Logger.Debug("AvProEdgeAvSwitchTcp - decoder {0} online status changed: {1}", a.Arg1, a.Arg2);
					this.AvIpEndpointStatusChanged?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, a.Arg1));
				};

				decoder.VideoRouteChanged += (o, e) =>
				{
					this.VideoRouteChanged?.Invoke(this, new GenericDualEventArgs<string, uint>(this.Id, e.Arg2));
				};

				decoder.Initialize();
			}
		}

		private void ComProcessorCommChangeHandler(object sender, AVProEdgeMXNetLib.EventArguments.AnaEventArgs args)
		{
			Logger.Debug("AvProEdgeSwitchTcp {0} - CommProcessorCommChangeHandler: {1}", this.Id, args.Payload);
			this.IsOnline = args.Payload > 0;
			var temp = this.ConnectionChanged;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}

		private void ComProcessorInitChangeHandler(object sender, AVProEdgeMXNetLib.EventArguments.AnaEventArgs args)
		{
			this.IsInitialized = args.Payload > 0;
		}

		private void ComProcessorReadyChangeHandler(object sender, EventArgs e)
		{
			this.commReady = true;
			Logger.Debug("AvProEdgeAvSwitchTcp {0} - Communication processor reports ready.", this.Id);
			this.Connect();
		}
	}
}
