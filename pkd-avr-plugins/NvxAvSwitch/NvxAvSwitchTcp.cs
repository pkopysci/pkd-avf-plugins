namespace NvxAvSwitch
{
	using Crestron.SimplSharpPro;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_hardware_service.AvIpMatrix;
	using pkd_hardware_service.AvSwitchDevices;
	using NvxAvSwitch.DataObjects;
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// Control class for Crestron NVX endpoints. This class does not use a NVX director. 
	/// Routing is handled by passing the Encoder stream address to the target decoder.
	/// </summary>
	public class NvxAvSwitchTcp : IAvSwitcher, IDisposable, IAvIpMatrix
	{
		private readonly Dictionary<string, NvxIpEndpoint> encoders;
		private readonly Dictionary<uint, NvxIpEndpoint> decoders;
		private readonly List<NvxSource> sources;
		private readonly List<IDisposable> disposables;
		private bool disposed;

		/// <summary>
		/// Creates a new instance of <see cref="NvxAvSwitchTcp"/>.
		/// All endpoint devices must be added becore calling Initialize(). Initialize() can only be called once.
		/// </summary>
		public NvxAvSwitchTcp()
		{
			this.encoders = new Dictionary<string, NvxIpEndpoint>();
			this.decoders = new Dictionary<uint, NvxIpEndpoint>();
			this.sources = new List<NvxSource>();
			this.disposables = new List<IDisposable>();
		}

		~NvxAvSwitchTcp()
		{
			this.Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> ConnectionChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, string>> AvIpEndpointStatusChanged;

		/// <inheritdoc/>
		public event EventHandler<GenericDualEventArgs<string, uint>> VideoRouteChanged;

		/// <summary>
		/// Gets the unique ID of the device.
		/// </summary>
		public string Id { get; private set; }

		/// <summary>
		/// Gets the user friendly label of the device.
		/// </summary>
		public string Label { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the device is online or not.
		/// </summary>
		public bool IsOnline { get; private set; }

		/// <summary>
		/// Gets a value indicating whether or not the device has been initialized and ready to connect.
		/// </summary>
		public bool IsInitialized { get; private set; }

		/// <inheritdoc/>
		public IAvIpEndpoint GetAvIpEndpoint(string deviceId)
		{
			foreach (var kvp in this.encoders)
			{
				if (kvp.Value.Id.Equals(deviceId, StringComparison.InvariantCulture))
				{
					return kvp.Value;
				}
			}

			foreach (var kvp in this.decoders)
			{
				if (kvp.Value.Id.Equals(deviceId, StringComparison.InvariantCulture))
				{
					return kvp.Value;
				}
			}

			Logger.Error("NvxAvSwitchTcp {0} - GetAvIpEndpoint() - no endpoint found with ID {0}", this.Id, deviceId);
			return null;
		}

		/// <summary>
		/// Add an endcoder or decoder endpoint to the internal control logic.
		/// Tags must include the class name of NVX endpoint prepended with 'nvx-' (Examples: nvx-DmNvxD30, nvx-DmNvx350, nvx-DmNvxE30, etc.).
		/// Tags must also include the IP-ID used for control with the format 'IPID-[hex value] (example: IPID-12).
		/// </summary>
		/// <param name="id">The unique ID of the endpoint to control.</param>
		/// <param name="tags">the collection of configuration tags. This should include the model name (see summary)</param>
		/// <param name="ioIndex">intput or output number of the NVX device as set in the configuration file.</param>
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
				if (!this.encoders.TryGetValue(tagData.NvxId, out var endpoint))
				{
					endpoint = new NvxIpEndpoint(tagData.NvxId, tagData.IpId, endpointType, tagData.ClassName, control);
					this.encoders.Add(endpoint.Id, endpoint);
					this.SubscribeEncoder(endpoint);
				}

				var srcData = new NvxSource() { Id = id, Endpoint = endpoint, NvxInputPort = tagData.InputPort, RouterIndex = (uint)ioIndex };
				this.sources.Add(srcData);
			}
			else if (endpointType == AvIpEndpointTypes.Decoder)
			{
				if (!this.decoders.TryGetValue((uint)ioIndex, out _))
				{
					NvxIpEndpoint endpoint = new NvxIpEndpoint(tagData.NvxId, tagData.IpId, endpointType, tagData.ClassName, control);
					this.decoders.Add((uint)ioIndex, endpoint);
					this.SubscribeDecoder(endpoint);
				}
			}
			else
			{
				Logger.Error("NvxAVSwitchTcp {0} - AddEndpoint()- unsupported endpoint type: {1}", this.Id, endpointType);
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
			if (this.IsInitialized)
			{
				Logger.Warn("NvxAvSwitchTcp {0} already initialized.", this.Id);
				return;
			}

			this.Id = id;
			this.Label = label;
			foreach (var kvp in encoders)
			{
				kvp.Value.Initialize();
			}

			foreach (var kvp in decoders)
			{
				kvp.Value.Initialize();
			}

			this.IsInitialized = true;
		}

		/// <inheritdoc/>
		public void Connect()
		{
			this.IsOnline = true;
			this.Notify(this.ConnectionChanged);
		}

		/// <inheritdoc/>
		public void Disconnect()
		{
			this.IsOnline = false;
			this.Notify(this.ConnectionChanged);
		}

		/// <inheritdoc/>
		public uint GetCurrentVideoSource(uint output)
		{
			if (!this.CheckInitialized("GetCurrentVideoRoute"))
			{
				return 0;
			}

			if (output > this.decoders.Count)
			{
				Logger.Error("NvxAvSwitchTcp {0} - GetCurrentVideoSource() - argument {1} is out of bounds.", this.Id, output);
				return 0;
			}

			if (this.decoders.TryGetValue(output, out NvxIpEndpoint decoder))
			{
				foreach (var source in this.sources)
				{
					bool onEncoder = source.Endpoint.SubscriptionUrl.Equals(decoder.SubscriptionUrl, StringComparison.Ordinal);
					bool onInputPort = source.NvxInputPort == source.Endpoint.InputSelected;
					if (onEncoder && onInputPort)
					{
						return source.RouterIndex;
					}
				}
			}

			Logger.Error("NvxAVSwitchTcp {0} - Cannot find decoder with the output index of {1}", this.Id, output);
			return 0;
		}

		/// <inheritdoc/>
		public void RouteVideo(uint source, uint output)
		{
			Logger.Debug("NvxAvSwitchTcp {0} - RouteVideo({1}, {2})", this.Id, source, output);

			if (!this.CheckInitialized("RouteVideo"))
			{
				return;
			}

			// subswitching on encoder
			var sourceData = this.sources.FirstOrDefault(x => x.RouterIndex == source);
			if (sourceData == null)
			{
				Logger.Error("NvxAvSwitchTcp {0} - RouteVideo() - Cannot find source with index {1}", this.Id, source);
				return;
			}

			if (sourceData.Endpoint.SupportsInputSelection)
			{
				sourceData.Endpoint.SelectInput(sourceData.NvxInputPort);
			}

			// set the decoder to the target encoder stream
			if (this.decoders.TryGetValue(output, out NvxIpEndpoint decoder))
			{
				decoder.SubscriptionUrl = sourceData.Endpoint.SubscriptionUrl;
			}
			else
			{
				Logger.Error("NvxAvSwitchTcp {0} - RouteVideo() - Cannot find decoder with index {1}", this.Id, output);
			}
		}

		/// <inheritdoc/>
		public void ClearVideoRoute(uint output)
		{
			if (!this.CheckInitialized("ClearVideoRoute"))
			{
				return;
			}

			if (this.decoders.TryGetValue(output, out NvxIpEndpoint decoder))
			{
				decoder.SubscriptionUrl = string.Empty;
			}
			else
			{
				Logger.Error("NvxAvSwitchTcp {0} - ClearVideoRoute() - Cannot find decoder with index {1}", this.Id, output);
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (this.disposed)
			{
				return;
			}

			if (disposing)
			{
				foreach (var disposable in disposables)
				{
					disposable.Dispose();
				}
			}

			this.disposed = true;
		}

		private void SubscribeDecoder(NvxIpEndpoint endpoint)
		{
			endpoint.OnlineStatusChanged += this.EndpointOnlineStatusChangedHandler;
			endpoint.StreamChanged += this.DecoderStreamChangedHandler;
			this.disposables.Add(endpoint);
		}

		private void SubscribeEncoder(NvxIpEndpoint endpoint)
		{
			endpoint.OnlineStatusChanged += this.EndpointOnlineStatusChangedHandler;
			if (endpoint.SupportsInputSelection)
			{
				endpoint.InputSelectionChanged += this.EndpointInputSelectionChangedHandler;
			}

			this.disposables.Add(endpoint);
		}

		private void EndpointInputSelectionChangedHandler(object sender, GenericSingleEventArgs<string> e)
		{
			Logger.Debug("NvxAvSwitchTcp {0} - Endpoint input changed for device {1}", this.Id, e.Arg);
			if (this.encoders.TryGetValue(e.Arg, out NvxIpEndpoint ep))
			{
				foreach (var kvp in this.decoders)
				{
					if (kvp.Value.SubscriptionUrl.Equals(ep.SubscriptionUrl, StringComparison.Ordinal))
					{
						this.Notify(this.VideoRouteChanged, kvp.Key);
					}
				}
			}
		}

		private void DecoderStreamChangedHandler(object sender, GenericSingleEventArgs<string> e)
		{
			Logger.Debug("NvxAvSwitchTcp {0} - Endpoint stream subscription changed for {1}", this.Id, e.Arg);
			foreach (var kvp in this.decoders)
			{
				if (kvp.Value.Id.Equals(e.Arg, StringComparison.InvariantCulture))
				{
					this.Notify(this.VideoRouteChanged, kvp.Key);
					break;
				}
			}
		}

		private void EndpointOnlineStatusChangedHandler(object sender, GenericSingleEventArgs<string> e)
		{
			var temp = this.AvIpEndpointStatusChanged;
			temp?.Invoke(this, new GenericDualEventArgs<string, string>(this.Id, e.Arg));
		}

		private void Notify(EventHandler<GenericSingleEventArgs<string>> handler)
		{
			var temp = handler;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}

		private void Notify(EventHandler<GenericDualEventArgs<string, uint>> handler, uint arg2)
		{
			var temp = handler;
			temp?.Invoke(this, new GenericDualEventArgs<string, uint>(this.Id, arg2));
		}

		private bool CheckInitialized(string methodName)
		{
			if (!this.IsInitialized)
			{
				Logger.Error("NvxAvSwitchTcp {0} - {1}() - Device not initialized.", this.Id, methodName);
				return false;
			}

			return true;
		}
	}
}
