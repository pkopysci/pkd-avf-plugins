namespace NvxAvSwitch;

using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DM;
using Crestron.SimplSharpPro.DM.Streaming;
using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Logging;
using pkd_hardware_service.AvIpMatrix;
using System;

internal class NvxIpEndpoint : IAvIpEndpoint, IDisposable
{
	private readonly DmNvxBaseClass _endpoint;
	private bool _disposed;

	/// <summary>
	/// Creates an instance of <see cref="NvxIpEndpoint"/>.
	/// </summary>
	/// <param name="id">The unique ID associated with this device. </param>
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
		Id = id;
		EndpointType = endpointType;
		_endpoint = NvxEndpointFactory.CreateNvxBase(ipId, control, classType);
		SupportsInputSelection = CheckNvxType(_endpoint);
	}

	~NvxIpEndpoint()
	{
		Dispose(false);
	}

	/// <summary>
	/// Triggered whenever the connection status of the NVX endpoint changes.
	/// </summary>
	public event EventHandler<GenericSingleEventArgs<string>>? OnlineStatusChanged;

	/// <summary>
	/// Triggered when the device multicast stream address reports a change, either from being manually set elsewhere or due to a routing
	/// event.
	/// </summary>
	public event EventHandler<GenericSingleEventArgs<string>>? StreamChanged;

	/// <summary>
	/// Triggered whenever a change on the physical input selection is reported by the hardware.
	/// args is the ID of this NvxIpEndpoint.
	/// </summary>
	public event EventHandler<GenericSingleEventArgs<string>>? InputSelectionChanged;

	/// <inheritdoc/>
	public bool IsOnline => _endpoint.IsOnline;

	/// <inheritdoc/>
	public AvIpEndpointTypes EndpointType { get; }

	/// <summary>
	/// Gets the unique ID associated with this device.
	/// </summary>
	public string Id { get; }

	/// <summary>
	/// Gets a value indicating whether this device supports the selection of physical inputs.
	/// </summary>
	public bool SupportsInputSelection { get; }

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
		get => _endpoint.Control.ServerUrlFeedback.StringValue;

		set
		{
			if (EndpointType != AvIpEndpointTypes.Decoder)
			{
				Logger.Error("NvxIpEndpoint {0} - SubscriptionUrl - Cannot change subscription on an encoder endpoint.", Id);
				return;
			}

			_endpoint.Control.ServerUrl.StringValue = value;
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
		if (!SupportsInputSelection)
		{
			Logger.Error("NvxIpEndpoint {0} - SelectInput() - input selection not supported.", Id);
			return;
		}

		if (input > 3)
		{
			Logger.Error("NvxIpEndpoint {0} = SelectInput() - Argument '{1}' is out of range.", Id, input);
			return;
		}

		switch (input)
		{
			case 0:
				_endpoint.Control.VideoSource = eSfpVideoSourceTypes.Disable;
				break;
			case 1:
				_endpoint.Control.VideoSource = eSfpVideoSourceTypes.Hdmi1;
				break;
			case 2:
				_endpoint.Control.VideoSource = eSfpVideoSourceTypes.Hdmi2;
				break;
			case 3:
				_endpoint.Control.VideoSource = eSfpVideoSourceTypes.Stream;
				break;
		}
	}

	/// <summary>
	/// Set all event handlers and register the NVX endpoint with the control system.
	/// </summary>
	public void Initialize()
	{
		_endpoint.BaseEvent += EndpointBaseEventHandler;
		_endpoint.OnlineStatusChange += EndpointConnectionHandler;
		if (_endpoint.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
		{
			Logger.Error("Failed to register NVX endpoint with ID {0} - {1}", Id, _endpoint.RegistrationFailureReason);
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
			_endpoint.OnlineStatusChange -= EndpointConnectionHandler;
			_endpoint.BaseEvent -= EndpointBaseEventHandler;
			_endpoint.UnRegister();
			_endpoint.Dispose();
		}

		_disposed = true;
	}

	private void Notify(EventHandler<GenericSingleEventArgs<string>>? handler)
	{
		handler?.Invoke(this, new GenericSingleEventArgs<string>(Id));
	}

	private void EndpointConnectionHandler(GenericBase currentDevice, OnlineOfflineEventArgs args)
	{
		Notify(OnlineStatusChanged);
	}

	private void EndpointBaseEventHandler(GenericBase device, BaseEventArgs args)
	{
		switch (args.EventId)
		{
			case DMInputEventIds.ServerUrlEventId:
				Notify(StreamChanged);
				break;
			case DMInputEventIds.VideoSourceEventId:
				UpdateSelectedInput();
				Notify(InputSelectionChanged);
				break;
		}
	}

	private bool CheckNvxType(DmNvxBaseClass nvx)
	{
		return nvx switch
		{
			DmNvx35x or DmNvxD10D20Base or DmNvxE10E20Base => true,
			_ => false
		};
	}

	private void UpdateSelectedInput()
	{
		if (!SupportsInputSelection)
		{
			return;
		}

		InputSelected = _endpoint.Control.VideoSourceFeedback switch
		{
			eSfpVideoSourceTypes.Disable => 0,
			eSfpVideoSourceTypes.Hdmi1 => 1,
			eSfpVideoSourceTypes.Hdmi2 => 2,
			eSfpVideoSourceTypes.Stream => 3,
			_ => InputSelected
		};
	}
}