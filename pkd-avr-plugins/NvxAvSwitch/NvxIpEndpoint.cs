namespace NvxAvSwitch
{
	using Crestron.SimplSharpPro;
	using Crestron.SimplSharpPro.DM;
	using Crestron.SimplSharpPro.DM.Streaming;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_hardware_service.AvIpMatrix;
	using System;

	internal class NvxIpEndpoint : IAvIpEndpoint, IDisposable
	{
		private readonly DmNvxBaseClass endpoint;
		private readonly AvIpEndpointTypes epType;
		[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "Future use")]
		private readonly string classType;
		private bool disposed;

		/// <summary>
		/// Creates an instance of <see cref="NvxIpEndpoint"/>.
		/// </summary>
		/// <param name="id">The unique ID associated with this device. </param>
		/// <param name="label">The user-readable label associated with this device.</param>
		/// <param name="ipId">IP-ID used by the control system when manipulating the hardware.</param>
		/// <param name="endpointType">Defines whether this is an encoder or decoder IP device</param>
		/// <param name="classType">The class name of the NVX model being controlled.</param>
		/// <param name="control">The root Crestron control system that will connect with the hardware.</param>
		public NvxIpEndpoint(
			string id,
			uint ipId,
			AvIpEndpointTypes endpointType,
			string classType,
			CrestronControlSystem control)
		{
			this.Id = id;
			this.epType = endpointType;
			this.classType = classType;
			this.endpoint = NvxEndpointFactory.CreateNvxBase(ipId, control, classType);
			this.SupportsInputSelection = this.CheckNvxType(this.endpoint);
		}

		~NvxIpEndpoint()
		{
			this.Dispose(false);
		}

		/// <inheritdoc/>
		public event EventHandler<GenericSingleEventArgs<string>> OnlineStatusChanged;

		/// <summary>
		/// Triggered when the device multicast stream address reports a change, either from being manually set elsewhere or due to a routing
		/// event.
		/// </summary>
		public event EventHandler<GenericSingleEventArgs<string>> StreamChanged;

		/// <summary>
		/// Triggered whenever a change on the physical input selction is reported by the hardware.
		/// args is the ID of this NvxIpEndpoint.
		/// </summary>
		public event EventHandler<GenericSingleEventArgs<string>> InputSelectionChanged;

		/// <inheritdoc/>
		public bool IsOnline { get { return this.endpoint.IsOnline; } }

		/// <inheritdoc/>
		public AvIpEndpointTypes EndpointType { get { return this.epType; } }

		/// <summary>
		/// Gets the unique ID associated with this device.
		/// </summary>
		public string Id { get; private set; }

		/// <summary>
		/// Gets a value indicating whether or not this device supports the selection of physical inputs.
		/// </summary>
		public bool SupportsInputSelection { get; private set; }

		/// <summary>
		/// returns the currently selected input number as of the last report from the hardware.
		/// 0 = disabled, 1 = HDMI 1, 2 = HDMI 2, 3 = Stream.
		/// </summary>
		public uint InputSelected { get; private set; }

		/// <summary>
		/// Gets or sets the multicast address used for AV distribution. If this is an encoder then the value returned will be the
		/// broadcast address for the stream. If this is a decoder then it will return the currently subscribed stream.
		/// Setting this value on an encoder will have no affect and will log an error to the logging system.
		/// </summary>
		public string SubscriptionUrl
		{
			get
			{
				return this.endpoint.Control.ServerUrlFeedback.StringValue;
			}

			set
			{
				if (this.epType != AvIpEndpointTypes.Decoder)
				{
					Logger.Error("NvxIpEndpoint {0} - SetSubcription() - Cannot change subscription on an encoder endpoint.", this.Id);
					return;
				}

				this.endpoint.Control.ServerUrl.StringValue = value;
			}
		}

		/// <summary>
		/// Sets the selected physical input on the device is that feature is supported (check SupportsInputSelection).
		/// If input selection is not supported then an error is written to the logging system.
		/// If the argument for 'input' is out of range then an error is written to the logging system.
		/// </summary>
		/// <param name="input">0 = disable, 1 = HDMI 1, 2 = HDMI 2, 3 = stream</param>
		public void SelectInput(uint input)
		{
			if (!this.SupportsInputSelection)
			{
				Logger.Error("NvxIpEndpoint {0} - SelectInput() - input selection not supported.", this.Id);
				return;
			}

			if (input > 3)
			{
				Logger.Error("NvxIpEndpoint {0} = SelectInput() - Argument '{1}' is out of range.", this.Id, input);
				return;
			}

			switch (input)
			{
				case 0:
					this.endpoint.Control.VideoSource = eSfpVideoSourceTypes.Disable;
					break;
				case 1:
					this.endpoint.Control.VideoSource = eSfpVideoSourceTypes.Hdmi1;
					break;
				case 2:
					this.endpoint.Control.VideoSource = eSfpVideoSourceTypes.Hdmi2;
					break;
				case 3:
					this.endpoint.Control.VideoSource = eSfpVideoSourceTypes.Stream;
					break;
			}
		}

		/// <inheritdoc/>
		public void Initialize()
		{
			if (this.endpoint == null)
			{
				Logger.Warn("Unable to Initialize endpoint {0} - device control does not exist.");
				return;
			}

			this.endpoint.BaseEvent += this.EndpointBaseEventHandler;
			this.endpoint.OnlineStatusChange += this.EndpointConnectionHandler;
			if (this.endpoint.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
			{
				Logger.Error("Failed to register NVX endpoint with ID {0} - {1}", this.Id, this.endpoint.RegistrationFailureReason);
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
				this.endpoint.OnlineStatusChange -= this.EndpointConnectionHandler;
				this.endpoint.BaseEvent -= this.EndpointBaseEventHandler;
				this.endpoint.UnRegister();
				this.endpoint.Dispose();
			}

			this.disposed = true;
		}

		private void Notify(EventHandler<GenericSingleEventArgs<string>> handler)
		{
			var temp = handler;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}

		private void EndpointConnectionHandler(GenericBase currentDevice, OnlineOfflineEventArgs args)
		{
			this.Notify(this.OnlineStatusChanged);
		}

		private void EndpointBaseEventHandler(GenericBase device, BaseEventArgs args)
		{
			switch (args.EventId)
			{
				case DMInputEventIds.ServerUrlEventId:
					this.Notify(this.StreamChanged);
					break;
				case DMInputEventIds.VideoSourceEventId:
					this.UpdateSelectedInput();
					this.Notify(this.InputSelectionChanged);
					break;
			}
		}

		private bool CheckNvxType(DmNvxBaseClass nvx)
		{
			if (nvx is DmNvx35x)
			{
				return true;
			}

			if (nvx is DmNvxD10D20Base)
			{
				return true;
			}

			if (nvx is DmNvxE10E20Base)
			{
				return true;
			}

			return false;
		}

		private void UpdateSelectedInput()
		{
			if (!this.SupportsInputSelection)
			{
				return;
			}

			switch (this.endpoint.Control.VideoSourceFeedback)
			{
				case eSfpVideoSourceTypes.Disable:
					this.InputSelected = 0;
					break;
				case eSfpVideoSourceTypes.Hdmi1:
					this.InputSelected = 1;
					break;
				case eSfpVideoSourceTypes.Hdmi2:
					this.InputSelected = 2;
					break;
				case eSfpVideoSourceTypes.Stream:
					this.InputSelected = 3;
					break;
			}
		}
	}
}
