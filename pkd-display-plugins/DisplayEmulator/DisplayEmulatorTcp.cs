namespace DisplayEmulator
{
	using pkd_common_utils.GenericEventArgs;
	using pkd_hardware_service.DisplayDevices;
	using pkd_hardware_service.Routable;
	using System;

	public class DisplayEmulatorTcp : IDisplayDevice, IVideoRoutable
	{
		private uint vidSource = 0;

		public event EventHandler<GenericSingleEventArgs<string>> VideoBlankChanged;
		public event EventHandler<GenericSingleEventArgs<string>> HoursUsedChanged;
		public event EventHandler<GenericSingleEventArgs<string>> PowerChanged;
		public event EventHandler<GenericSingleEventArgs<string>> ConnectionChanged;
		public event EventHandler<GenericSingleEventArgs<string>> VideoFreezeChanged;
		public event EventHandler<GenericDualEventArgs<string, uint>> VideoRouteChanged;

		public bool PowerState { get; private set; }

		public bool EnableReconnect { get; set; }

		public uint HoursUsed { get; private set; }

		public bool SupportsFreeze { get; private set; }

		public bool IsOnline { get; private set; }

		public string Label { get; private set; }

		public string Id { get; private set; }

		public bool FreezeState { get; private set; }

		public bool BlankState { get; private set; }

		public bool IsInitialized { get; private set; }

		public void DisablePolling() { }

		public void EnablePolling() { }

		public void Initialize(string ipAddress, int port, string label, string id)
		{
			this.Id = id;
			this.Label = label;
			this.SupportsFreeze = true;
			this.HoursUsed = 42;
			this.IsInitialized = true;
		}

		public void PowerOff()
		{
			if (this.IsOnline)
			{
				this.PowerState = false;
				this.Notify(this.PowerChanged);
			}
		}

		public void PowerOn()
		{
			if (this.IsOnline)
			{
				this.PowerState = true;
				this.Notify(this.PowerChanged);
			}
		}

		public void Connect()
		{
			if (this.IsOnline)
			{
				return;
			}

			this.IsOnline = true;
			this.Notify(this.ConnectionChanged);
		}

		public void Disconnect()
		{
			if (!this.IsOnline)
			{
				return;
			}

			this.IsOnline = false;
			this.Notify(this.ConnectionChanged);
		}

		public void FreezeOff()
		{
			if (!this.FreezeState)
			{
				return;
			}

			this.FreezeState = false;
			this.Notify(this.VideoFreezeChanged);
		}

		public void FreezeOn()
		{
			if (this.FreezeState)
			{
				return;
			}

			this.FreezeState = true;
			this.Notify(this.VideoFreezeChanged);
		}

		public void VideoBlankOff()
		{
			if (!this.BlankState)
			{
				return;
			}

			this.BlankState = false;
			this.Notify(this.VideoBlankChanged);
		}

		public void VideoBlankOn()
		{
			if (this.BlankState)
			{
				return;
			}

			this.BlankState = true;
			this.Notify(this.VideoBlankChanged);
		}

		public void ClearVideoRoute(uint output)
		{
			this.vidSource = 0;
			this.Notify(this.VideoRouteChanged);

		}

		public uint GetCurrentVideoSource(uint output)
		{
			return this.vidSource;
		}

		public void RouteVideo(uint source, uint output)
		{
			if (!this.IsOnline || !this.PowerState)
			{
				return;
			}

			this.vidSource = source;
			this.Notify(this.VideoRouteChanged);
		}

		private void Notify(EventHandler<GenericSingleEventArgs<string>> handler)
		{
			var temp = handler;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}

		private void Notify(EventHandler<GenericDualEventArgs<string, uint>> handler)
		{
			var temp = handler;
			temp?.Invoke(this, new GenericDualEventArgs<string, uint>(this.Id, 1));
		}

	}
}
