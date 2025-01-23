namespace DisplayEmulator;

using pkd_common_utils.GenericEventArgs;
using pkd_hardware_service.DisplayDevices;
using pkd_hardware_service.Routable;
using System;

public class DisplayEmulatorTcp : IDisplayDevice, IVideoRoutable
{
	private uint _vidSource;

	public event EventHandler<GenericSingleEventArgs<string>>? VideoBlankChanged;
	public event EventHandler<GenericSingleEventArgs<string>>? HoursUsedChanged;
	public event EventHandler<GenericSingleEventArgs<string>>? PowerChanged;
	public event EventHandler<GenericSingleEventArgs<string>>? ConnectionChanged;
	public event EventHandler<GenericSingleEventArgs<string>>? VideoFreezeChanged;
	public event EventHandler<GenericDualEventArgs<string, uint>>? VideoRouteChanged;

	public bool PowerState { get; private set; }

	public bool EnableReconnect { get; set; }

	public uint HoursUsed { get; private set; }

	public bool SupportsFreeze { get; private set; }

	public bool IsOnline { get; private set; }

	public string Label { get; private set; } = string.Empty;

	public string Id { get; private set; } = string.Empty;

	public bool FreezeState { get; private set; }

	public bool BlankState { get; private set; }

	public bool IsInitialized { get; private set; }

	public void DisablePolling() { }

	public void EnablePolling() { }

	public void Initialize(string ipAddress, int port, string label, string id)
	{
		Id = id;
		Label = label;
		SupportsFreeze = true;
		HoursUsed = 42;
		IsInitialized = true;
	}

	public void PowerOff()
	{
		if (!IsOnline) return;
		PowerState = false;
		Notify(PowerChanged);
	}

	public void PowerOn()
	{
		if (!IsOnline) return;
		PowerState = true;
		Notify(PowerChanged);
	}

	public void Connect()
	{
		if (IsOnline)
		{
			return;
		}

		IsOnline = true;
		Notify(ConnectionChanged);
	}

	public void Disconnect()
	{
		if (!IsOnline)
		{
			return;
		}

		IsOnline = false;
		Notify(ConnectionChanged);
	}

	public void FreezeOff()
	{
		if (!FreezeState)
		{
			return;
		}

		FreezeState = false;
		Notify(VideoFreezeChanged);
	}

	public void FreezeOn()
	{
		if (FreezeState)
		{
			return;
		}

		FreezeState = true;
		Notify(VideoFreezeChanged);
	}

	public void VideoBlankOff()
	{
		if (!BlankState)
		{
			return;
		}

		BlankState = false;
		Notify(VideoBlankChanged);
	}

	public void VideoBlankOn()
	{
		if (BlankState)
		{
			return;
		}

		BlankState = true;
		Notify(VideoBlankChanged);
	}

	public void ClearVideoRoute(uint output)
	{
		_vidSource = 0;
		Notify(VideoRouteChanged);

	}

	public uint GetCurrentVideoSource(uint output)
	{
		return _vidSource;
	}

	public void RouteVideo(uint source, uint output)
	{
		if (!IsOnline || !PowerState)
		{
			return;
		}

		_vidSource = source;
		Notify(VideoRouteChanged);
	}

	private void Notify(EventHandler<GenericSingleEventArgs<string>>? handler)
	{
		handler?.Invoke(this, new GenericSingleEventArgs<string>(Id));
	}

	private void Notify(EventHandler<GenericDualEventArgs<string, uint>>? handler)
	{
		handler?.Invoke(this, new GenericDualEventArgs<string, uint>(Id, 1));
	}
}