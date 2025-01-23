namespace AvProEdgeAvSwitch.AvpEndpoints;

using pkd_common_utils.GenericEventArgs;
using pkd_common_utils.Validation;
using pkd_hardware_service.AvIpMatrix;
using System;

internal abstract class AvpEndpoint : IAvIpEndpoint
{
	/// <summary>
	/// creates an instance of <see cref="AvpEndpoint"/> class.
	/// </summary>
	/// <param name="macAddress">The unique MAC address of the endpoint that will be controlled.</param>
	/// <param name="processorId">The MXNet CBox ID that will handle endpoint management for this device.</param>
	/// <param name="index">The input or output index of the underlying endpoint.</param>
	public AvpEndpoint(string macAddress, ushort processorId, ushort index)
	{
		ParameterValidator.ThrowIfNullOrEmpty(macAddress, "AvpEndpoint.Ctor", nameof(macAddress));
		MacAddress = macAddress;
		ProcessorId = processorId;
		Index = index;
	}

	/// <summary>
	/// Triggered whenever the underlying endpoint device is initialized.
	/// </summary>
	public event EventHandler<GenericDualEventArgs<string, bool>>? InitializationChanged;

	/// <summary>
	/// Triggered when a connection is established or broken with the underlying endpoint device.
	/// </summary>
	public event EventHandler<GenericDualEventArgs<string, bool>>? ConnectionStatusChanged;
	
	/// <inheritdoc/>
	public AvIpEndpointTypes EndpointType { get; protected init; }

	/// <summary>
	/// Gets the unique MAC address of the endpoint that will be controlled.
	/// </summary>
	public string MacAddress { get; }

	/// <summary>
	/// Gets the MXNet CBox ID that will handle endpoint management for this device.
	/// </summary>
	public ushort ProcessorId { get; }

	/// <summary>
	/// Gets the input or output index of the underlying endpoint.
	/// </summary>
	public ushort Index { get; }

	/// <inheritdoc/>
	public virtual bool IsOnline { get; protected set; }

	/// <summary>
	/// Gets a value indicating whether the underlying device has been initialized.
	/// </summary>
	public virtual bool IsInitialized { get; protected set; }

	/// <summary>
	/// Configures the underlying endpoint and attempts to establish a connection.
	/// </summary>
	public abstract void Initialize();

	protected void NotifyInitializeChanged()
	{
		var temp = InitializationChanged;
		temp?.Invoke(this, new GenericDualEventArgs<string, bool>(MacAddress, IsInitialized));
	}

	protected void NotifyOnlineChanged()
	{
		var temp = ConnectionStatusChanged;
		temp?.Invoke(this, new GenericDualEventArgs<string, bool>(MacAddress, IsOnline));
	}
}