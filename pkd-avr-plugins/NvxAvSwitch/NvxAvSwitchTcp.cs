namespace NvxAvSwitch
{
	using Crestron.SimplSharpPro;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_hardware_service.AvIpMatrix;
	using pkd_hardware_service.AvSwitchDevices;
	using DataObjects;
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// Control class for Crestron NVX endpoints. This class does not use a NVX director. 
	/// Routing is handled by passing the Encoder stream address to the target decoder.
	/// </summary>
	public class NvxAvSwitchTcp : IAvSwitcher, IDisposable, IAvIpMatrix
	{
		private readonly Dictionary<string, NvxIpEndpoint> _encoders;
		private readonly Dictionary<uint, NvxIpEndpoint> _decoders;
		private readonly List<NvxSource> _sources;
		private readonly List<IDisposable> _disposables;
		private bool _disposed;

		/// <summary>
		/// Creates a new instance of <see cref="NvxAvSwitchTcp"/>.
		/// All endpoint devices must be added before calling Initialize(). Initialize() can only be called once.
		/// </summary>
		public NvxAvSwitchTcp()
		{
			_encoders = new Dictionary<string, NvxIpEndpoint>();
			_decoders = new Dictionary<uint, NvxIpEndpoint>();
			_sources = [];
			_disposables = [];
		}

		~NvxAvSwitchTcp()
		{
			Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>>? ConnectionChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>>? AvIpEndpointStatusChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, uint>>? VideoRouteChanged;

		/// <summary>
		/// Gets the unique ID of the device.
		/// </summary>
		public string Id { get; private set; } = string.Empty;

		/// <summary>
		/// Gets the human-friendly label of the device.
		/// </summary>
		public string Label { get; private set; } = string.Empty;

		/// <summary>
		/// Gets a value indicating whether the device is online or not.
		/// </summary>
		public bool IsOnline { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the device has been initialized and ready to connect.
		/// </summary>
		public bool IsInitialized { get; private set; }

		/// <inheritdoc/>
		public IAvIpEndpoint? GetAvIpEndpoint(string deviceId)
		{
			foreach (var kvp in _encoders)
			{
				if (kvp.Value.Id.Equals(deviceId, StringComparison.InvariantCulture))
				{
					return kvp.Value;
				}
			}

			foreach (var kvp in _decoders)
			{
				if (kvp.Value.Id.Equals(deviceId, StringComparison.InvariantCulture))
				{
					return kvp.Value;
				}
			}

			Logger.Error("NvxAvSwitchTcp {0} - GetAvIpEndpoint() - no endpoint found with ID {0}", Id, deviceId);
			return null;
		}

		/// <summary>
		/// Add an encoder or decoder endpoint to the internal control logic.
		/// Tags must include the class name of NVX endpoint prepended with 'nvx-' (Examples: nvx-DmNvxD30, nvx-DmNvx350, nvx-DmNvxE30, etc.).
		/// Tags must also include the IP-ID used for control with the format 'IPID-[hex value]' (example: IPID-12).
		/// </summary>
		/// <param name="id">The unique ID of the endpoint to control.</param>
		/// <param name="tags">the collection of configuration tags. This should include the model name (see summary)</param>
		/// <param name="ioIndex">input or output number of the NVX device as set in the configuration file.</param>
		/// <param name="endpointType">defines if the endpoint is a decoder or encoder.</param>
		/// <param name="control">The host control system that will connect with the endpoint hardware.</param>
		public void AddEndpoint(
			string id,
			List<string> tags,
			int ioIndex,
			AvIpEndpointTypes endpointType,
			CrestronControlSystem control)
		{
			var tagData = NvxEndpointFactory.ParseTagData(tags);
			if (endpointType == AvIpEndpointTypes.Encoder)
			{
				if (!_encoders.TryGetValue(tagData.NvxId, out var endpoint))
				{
					endpoint = new NvxIpEndpoint(tagData.NvxId, tagData.IpId, endpointType, tagData.ClassName, control);
					_encoders.Add(endpoint.Id, endpoint);
					SubscribeEncoder(endpoint);
				}

				var srcData = new NvxSource() { Id = id, Endpoint = endpoint, NvxInputPort = tagData.InputPort, RouterIndex = (uint)ioIndex };
				_sources.Add(srcData);
			}
			else if (endpointType == AvIpEndpointTypes.Decoder)
			{
				if (_decoders.ContainsKey((uint)ioIndex)) return;
				var endpoint = new NvxIpEndpoint(tagData.NvxId, tagData.IpId, endpointType, tagData.ClassName, control);
				_decoders.Add((uint)ioIndex, endpoint);
				SubscribeDecoder(endpoint);
			}
			else
			{
				Logger.Error("NvxAVSwitchTcp {0} - AddEndpoint()- unsupported endpoint type: {1}", Id, endpointType);
			}
		}

		/// <inheritdoc/>
		public void Initialize(
			string hostName,
			int port,
			string id,
			string label,
			int numInputs,
			int numOutputs)
		{
			if (IsInitialized)
			{
				Logger.Warn("NvxAvSwitchTcp {0} already initialized.", Id);
				return;
			}

			Id = id;
			Label = label;
			foreach (var kvp in _encoders)
			{
				kvp.Value.Initialize();
			}

			foreach (var kvp in _decoders)
			{
				kvp.Value.Initialize();
			}

			IsInitialized = true;
		}

		/// <inheritdoc/>
		public void Connect()
		{
			IsOnline = true;
			Notify(ConnectionChanged);
		}

		/// <inheritdoc/>
		public void Disconnect()
		{
			IsOnline = false;
			Notify(ConnectionChanged);
		}

		/// <inheritdoc/>
		public uint GetCurrentVideoSource(uint output)
		{
			if (!CheckInitialized("GetCurrentVideoRoute"))
			{
				return 0;
			}

			if (output > _decoders.Count)
			{
				Logger.Error("NvxAvSwitchTcp {0} - GetCurrentVideoSource() - argument {1} is out of bounds.", Id, output);
				return 0;
			}

			if (_decoders.TryGetValue(output, out var decoder))
			{
				foreach (var source in _sources)
				{
					bool onEncoder = source.Endpoint?.SubscriptionUrl.Equals(decoder.SubscriptionUrl, StringComparison.Ordinal) ?? false;
					bool onInputPort = source.NvxInputPort == source.Endpoint?.InputSelected;
					if (onEncoder && onInputPort)
					{
						return source.RouterIndex;
					}
				}
			}

			Logger.Error("NvxAVSwitchTcp {0} - Cannot find decoder with the output index of {1}", Id, output);
			return 0;
		}

		/// <inheritdoc/>
		public void RouteVideo(uint source, uint output)
		{
			Logger.Debug("NvxAvSwitchTcp {0} - RouteVideo({1}, {2})", Id, source, output);

			if (!CheckInitialized("RouteVideo"))
			{
				return;
			}

			// sub-switching on encoder
			var sourceData = _sources.FirstOrDefault(x => x.RouterIndex == source);
			if (sourceData == null)
			{
				Logger.Error("NvxAvSwitchTcp {0} - RouteVideo() - Cannot find source with index {1}", Id, source);
				return;
			}

			if (sourceData.Endpoint == null) return;
			if (sourceData.Endpoint.SupportsInputSelection)
			{
				sourceData.Endpoint.SelectInput(sourceData.NvxInputPort);
			}

			// set the decoder to the target encoder stream
			if (_decoders.TryGetValue(output, out var decoder))
			{
				decoder.SubscriptionUrl = sourceData.Endpoint.SubscriptionUrl;
			}
			else
			{
				Logger.Error("NvxAvSwitchTcp {0} - RouteVideo() - Cannot find decoder with index {1}", Id, output);
			}
		}

		/// <inheritdoc/>
		public void ClearVideoRoute(uint output)
		{
			if (!CheckInitialized("ClearVideoRoute"))
			{
				return;
			}

			if (_decoders.TryGetValue(output, out var decoder))
			{
				decoder.SubscriptionUrl = string.Empty;
			}
			else
			{
				Logger.Error("NvxAvSwitchTcp {0} - ClearVideoRoute() - Cannot find decoder with index {1}", Id, output);
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}

			if (disposing)
			{
				foreach (var disposable in _disposables)
				{
					disposable.Dispose();
				}
			}

			_disposed = true;
		}

		private void SubscribeDecoder(NvxIpEndpoint endpoint)
		{
			endpoint.OnlineStatusChanged += EndpointOnlineStatusChangedHandler;
			endpoint.StreamChanged += DecoderStreamChangedHandler;
			_disposables.Add(endpoint);
		}

		private void SubscribeEncoder(NvxIpEndpoint endpoint)
		{
			endpoint.OnlineStatusChanged += EndpointOnlineStatusChangedHandler;
			if (endpoint.SupportsInputSelection)
			{
				endpoint.InputSelectionChanged += EndpointInputSelectionChangedHandler;
			}

			_disposables.Add(endpoint);
		}

		private void EndpointInputSelectionChangedHandler(object? sender, GenericSingleEventArgs<string> e)
		{
			Logger.Debug("NvxAvSwitchTcp {0} - Endpoint input changed for device {1}", Id, e.Arg);
			if (_encoders.TryGetValue(e.Arg, out var ep))
			{
				foreach (var kvp in _decoders)
				{
					if (kvp.Value.SubscriptionUrl.Equals(ep.SubscriptionUrl, StringComparison.Ordinal))
					{
						Notify(VideoRouteChanged, kvp.Key);
					}
				}
			}
		}

		private void DecoderStreamChangedHandler(object? sender, GenericSingleEventArgs<string> e)
		{
			Logger.Debug("NvxAvSwitchTcp {0} - Endpoint stream subscription changed for {1}", Id, e.Arg);
			foreach (var kvp in _decoders)
			{
				if (kvp.Value.Id.Equals(e.Arg, StringComparison.InvariantCulture))
				{
					Notify(VideoRouteChanged, kvp.Key);
					break;
				}
			}
		}

		private void EndpointOnlineStatusChangedHandler(object? sender, GenericSingleEventArgs<string> e)
		{
			var temp = AvIpEndpointStatusChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(Id, e.Arg));
		}

		private void Notify(EventHandler<GenericSingleEventArgs<string>>? handler)
		{
			handler?.Invoke(this, new GenericSingleEventArgs<string>(Id));
		}

		private void Notify(EventHandler<GenericDualEventArgs<string, uint>>? handler, uint arg2)
		{
			handler?.Invoke(this, new GenericDualEventArgs<string, uint>(Id, arg2));
		}

		private bool CheckInitialized(string methodName)
		{
			if (!IsInitialized)
			{
				Logger.Error("NvxAvSwitchTcp {0} - {1}() - Device not initialized.", Id, methodName);
				return false;
			}

			return true;
		}
	}
}
