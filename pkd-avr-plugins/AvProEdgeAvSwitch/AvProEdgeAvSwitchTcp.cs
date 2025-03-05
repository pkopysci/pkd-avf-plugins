namespace AvProEdgeAvSwitch
{
	using AvpEndpoints;
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
		private const ushort PollTime = 60;
		private const ushort DefaultId = 1;
		private readonly CommandProcessor _comProcessor;
		private readonly ControlBoxComponent _controlBox;
		private readonly List<EncoderAvpEndpoint> _encoders;
		private readonly List<DecoderAvpEndpoint> _decoders;
		private bool _commReady;
		private bool _disposed;

		/// <summary>
		/// Instantiates a new instance of <see cref="AvProEdgeAvSwitchTcp"/>. Call Initialize() to connect and begin device control.
		/// </summary>
		public AvProEdgeAvSwitchTcp()
		{
			_comProcessor = new CommandProcessor();
			_controlBox = new ControlBoxComponent();
			_encoders = [];
			_decoders = [];
		}

		~AvProEdgeAvSwitchTcp()
		{
			Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>>? ConnectionChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, uint>>? VideoRouteChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AvIpEndpointStatusChanged;

		/// <inheritdoc/>
		public string Id { get; private set; } = string.Empty;

		/// <inheritdoc/>
		public bool IsInitialized { get; private set; }

		/// <inheritdoc/>
		public bool IsOnline { get; private set; }

		/// <inheritdoc/>
		public string Label { get; private set; } = string.Empty;
		
		/// <inheritdoc/>
		public string Manufacturer { get; set; } = "Av Pro Edge";
		
		/// <inheritdoc/>
		public string Model { get; set; } = "MXNet";

		/// <inheritdoc/>
		public void AddEndpoint(string id, List<string> tags, int ioIndex, AvIpEndpointTypes endpointType, CrestronControlSystem control) { }

		/// <inheritdoc/>
		public void Initialize(string hostName, int port, string id, string label, int numInputs, int numOutputs)
		{
			ParameterValidator.ThrowIfNullOrEmpty(hostName, "AvProEdgeAvSwitchTcp.Initialize", nameof(hostName));
			ParameterValidator.ThrowIfNullOrEmpty(id, "AvProEdgeAvSwitchTcp.Initialize", nameof(id));

			Id = id;
			Label = label;

			var config = MxnetConfigReader.TryReadConfig(Id);
			if (config == null)
			{
				Logger.Error("AvProEdgeAvSwitchTcp.Initialize() - Failed to retrieve router configuration. Cannot initialize system.");
				return;
			}

			CreateAndSubscribeEncoders(config);
			CreateAndSubscribeDecoders(config);

			_comProcessor.OnReadyState += ComProcessorReadyChangeHandler;
			_comProcessor.OnInitializationChange += ComProcessorInitChangeHandler;
			_comProcessor.OnCommunicationChange += ComProcessorCommChangeHandler;
			_comProcessor.Configure(DefaultId, hostName, PollTime);

			_controlBox.Configure(DefaultId);
			_controlBox.ProcessIPAddress(hostName);
		}

		/// <inheritdoc/>
		public void Connect()
		{
			if (_commReady)
			{
				_comProcessor.Connect();
			}
		}

		/// <inheritdoc/>
		public void Disconnect()
		{
			if (_commReady)
			{
				_comProcessor.Disconnect();
			}
		}

		/// <inheritdoc/>
		public void ClearVideoRoute(uint output)
		{
			if (output > _decoders.Count)
			{
				Logger.Error("AvProEdgeSwitchTcp.ClearVideoRoute({0}) - output exceeds max outputs.", output);
				return;
			}

			_decoders[(int)output - 1].RouteSource(0);
		}

		/// <inheritdoc/>
		public uint GetCurrentVideoSource(uint output)
		{
			if (output > _decoders.Count)
			{
				Logger.Error("AvProEdgeSwitchTcp.GetCurrentVideoSource({0}) - output exceeds max outputs.", output);
				return 0;
			}

			return _decoders[(int)output - 1].CurrentVideoSource;
		}

		/// <inheritdoc/>
		public void RouteVideo(uint source, uint output)
		{
			Logger.Debug("AvProEdgeAvSwitchTcp.RouteVideo({0}, {1})", source, output);

			if (source > _encoders.Count)
			{
				Logger.Error("AvProEdgeSwitchTcp.RouteVideo({0}, {1}) - source exceeds max inputs.", source, output);
				return;
			}

			if (output > _decoders.Count)
			{
				Logger.Error("AvProEdgeSwitchTcp.RouteVideo({0}, {1}) - output exceeds max outputs.", source, output);
				return;
			}

			_decoders[(int)output - 1].RouteSource(source);
		}

		/// <inheritdoc/>
		public IAvIpEndpoint? GetAvIpEndpoint(string deviceId)
		{
			IAvIpEndpoint? endpoint = _encoders.FirstOrDefault(x => x.MacAddress.Equals(deviceId));
			if (endpoint != null) return endpoint;
			
			endpoint = _decoders.FirstOrDefault(x => x.MacAddress.Equals(deviceId));
			return endpoint;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					// TODO: dispose as necessary
				}

				_disposed = true;
			}
		}

		private void CreateAndSubscribeEncoders(MxnetConfig config)
		{
			for (int i = 0; i < config.InputEncoders.Count; i++)
			{
				var input = config.InputEncoders[i];
				var encoder = new EncoderAvpEndpoint(input, DefaultId, (ushort)(i + 1));
				_encoders.Add(encoder);

				encoder.InitializationChanged += (_, a) =>
				{
					Logger.Debug("AvProEdgeAvSwitchTcp - encoder {0} init status changed: {1}", a.Arg1, a.Arg2);
				};

				encoder.ConnectionStatusChanged += (_, a) =>
				{
					Logger.Debug("AvProEdgeAvSwitchTcp - encoder {0} online status changed: {1}", a.Arg1, a.Arg2);
					AvIpEndpointStatusChanged?.Invoke(this, new GenericDualEventArgs<string, string>(Id, a.Arg1));
				};

				encoder.Initialize();
			}
		}

		private void CreateAndSubscribeDecoders(MxnetConfig config)
		{
			for (int i = 0; i < config.OutputDecoders.Count; i++)
			{
				var output = config.OutputDecoders[i];
				var decoder = new DecoderAvpEndpoint(output.MacAddressAuto, DefaultId, (ushort)(i + 1));
				_decoders.Add(decoder);

				decoder.ConnectionStatusChanged += (_, a) =>
				{
					Logger.Debug("AvProEdgeAvSwitchTcp - decoder {0} online status changed: {1}", a.Arg1, a.Arg2);
					AvIpEndpointStatusChanged?.Invoke(this, new GenericDualEventArgs<string, string>(Id, a.Arg1));
				};

				decoder.VideoRouteChanged += (_, e) =>
				{
					VideoRouteChanged?.Invoke(this, new GenericDualEventArgs<string, uint>(Id, e.Arg2));
				};

				decoder.Initialize();
			}
		}

		private void ComProcessorCommChangeHandler(object sender, AVProEdgeMXNetLib.EventArguments.AnaEventArgs args)
		{
			Logger.Debug("AvProEdgeSwitchTcp {0} - CommProcessorCommChangeHandler: {1}", Id, args.Payload);
			IsOnline = args.Payload > 0;
			var temp = ConnectionChanged;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
		}

		private void ComProcessorInitChangeHandler(object sender, AVProEdgeMXNetLib.EventArguments.AnaEventArgs args)
		{
			IsInitialized = args.Payload > 0;
		}

		private void ComProcessorReadyChangeHandler(object? sender, EventArgs e)
		{
			_commReady = true;
			Logger.Debug("AvProEdgeAvSwitchTcp {0} - Communication processor reports ready.", Id);
			Connect();
		}
	}
}
