namespace DirecTvCableBox
{
	using Crestron.SimplSharp.CrestronIO;
	using Crestron.SimplSharp.Reflection;
	using Crestron.SimplSharpPro;
	using pkd_common_utils.GenericEventArgs;
	using pkd_common_utils.Logging;
	using pkd_common_utils.Validation;
	using pkd_hardware_service.TransportDevices;
	using System;

	/// <summary>
	/// Transport device implementation for controlling a DirecTV H25 cable box using IR.
	/// </summary>
	public class DirecTvCableBoxIr : ITransportDevice
	{
		private IROutputPort port;
		private const string DRIVER_NAME = "CableBox_DirecTV_H25-100.ir";
		private const int PULSE_TIME = 150;

		/// <summary>
		/// Instantiates a new instance of <see cref="DirecTvCableBoxIr"/>.
		/// </summary>
		public DirecTvCableBoxIr()
		{
			this.SupportsColorButtons = true;
			this.SupportsDiscretePower = true;
		}

		/// <inheritdoc />
		public event EventHandler<GenericSingleEventArgs<string>> ConnectionChanged;

		/// <inheritdoc />
		public string Id { get; private set; }

		/// <inheritdoc />
		public bool IsInitialized { get; private set; }

		/// <inheritdoc />
		public bool IsOnline { get; private set; }

		/// <inheritdoc />
		public string Label { get; private set; }

		/// <inheritdoc />
		public bool SupportsColorButtons { get; private set; }

		/// <inheritdoc />
		public bool SupportsDiscretePower { get; private set; }

		/// <inheritdoc />
		public void Initialize(IROutputPort port, string id, string label)
		{
			ParameterValidator.ThrowIfNull(port, "DirecTvCableBoxIr.Ctor", "port");
			ParameterValidator.ThrowIfNullOrEmpty(id, "DirecTvCableBoxIr.Ctor", "id");
			ParameterValidator.ThrowIfNullOrEmpty(label, "DirecTvCableBoxir.Ctor", "label");

			this.Id = id;
			this.Label = label;
			this.port = port;
			this.LoadIrFile();
			this.IsInitialized = true;
			this.IsOnline = true;
			var temp = this.ConnectionChanged;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(this.Id));
		}

		/// <inheritdoc />
		public void Connect() { }

		/// <inheritdoc />
		public void Disconnect() { }

		/// <inheritdoc />
		public void Back()
		{
			if (this.CheckInit("Back"))
			{
				this.port.PressAndRelease("BACK", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void Blue()
		{
			if (this.CheckInit("Blue"))
			{
				this.port.PressAndRelease("BLUE", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void ChannelDown()
		{
			if (this.CheckInit("ChannelDown"))
			{
				this.port.PressAndRelease("CH-", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void ChannelUp()
		{
			if (this.CheckInit("ChannelUp"))
			{
				this.port.PressAndRelease("CH+", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void Dash()
		{
			if (this.CheckInit("Dash"))
			{
				this.port.PressAndRelease("DIGIT_-", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void Digit(ushort digit)
		{
			if (!this.CheckInit("Digit"))
			{
				return;
			}

			this.port.PressAndRelease(digit.ToString(), PULSE_TIME);
		}

		/// <inheritdoc />
		public void Exit()
		{
			if (this.CheckInit("Exit"))
			{
				this.port.PressAndRelease("EXIT", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void Green()
		{
			if (this.CheckInit("Green"))
			{
				this.port.PressAndRelease("GREEN", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void Guide()
		{
			if (this.CheckInit("Guide"))
			{
				this.port.PressAndRelease("GUIDE", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void Info()
		{
			if (this.CheckInit("Info"))
			{
				this.port.PressAndRelease("INFO", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void Menu()
		{
			if (this.CheckInit("Menu"))
			{
				this.port.PressAndRelease("MENU", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void NavDown()
		{
			if (this.CheckInit("NavDown"))
			{
				this.port.PressAndRelease("DN_ARROW", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void NavLeft()
		{
			if (this.CheckInit("NavLeft"))
			{
				this.port.PressAndRelease("LEFT_ARROW", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void NavRight()
		{
			if (this.CheckInit("NavRight"))
			{
				this.port.PressAndRelease("RIGHT_ARROW", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void NavUp()
		{
			if (this.CheckInit("NavUp"))
			{
				this.port.PressAndRelease("UP_ARROW", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void PageDown()
		{
			if (this.CheckInit("PageDown"))
			{
				this.port.PressAndRelease("PAGE_DOWN", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void PageUp()
		{
			if (this.CheckInit("PageUp"))
			{
				this.port.PressAndRelease("PAGE_UP", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void Pause()
		{
			if (this.CheckInit("Pause"))
			{
				this.port.PressAndRelease("PAUSE", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void Play()
		{
			if (this.CheckInit("Play"))
			{
				this.port.PressAndRelease("PLAY", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void PowerOff()
		{
			if (this.CheckInit("PowerOff"))
			{
				this.port.PressAndRelease("POWER_OFF", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void PowerOn()
		{
			if (this.CheckInit("PowerOn"))
			{
				this.port.PressAndRelease("POWER_ON", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void PowerToggle()
		{
			if (this.CheckInit("PowerTOggle"))
			{
				Logger.Warn("DirecTvCableBoxIr {0} - PowerToggle() is not supported by this device.", this.Id);
			}
		}

		/// <inheritdoc />
		public void Record()
		{
			if (this.CheckInit("Record"))
			{
				this.port.PressAndRelease("RECORD", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void Red()
		{
			if (this.CheckInit("Red"))
			{
				this.port.PressAndRelease("RED", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void ScanForward()
		{
			if (this.CheckInit("ScanForward()"))
			{
				this.port.PressAndRelease("FSCAN", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void ScanReverse()
		{
			if (this.CheckInit("ScanReverse"))
			{
				this.port.PressAndRelease("RSCAN", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void SkipForward()
		{
			if (this.CheckInit("SkipForward"))
			{
				Logger.Warn("DirecTvCableBoxIr {0} - SkipForward() is not supported by this device.", this.Id);
			}
		}

		/// <inheritdoc />
		public void SkipReverse()
		{
			if (this.CheckInit("SkipForward"))
			{
				Logger.Warn("DirecTvCableBoxIr {0} - SkipReverse() is not supported by this device.", this.Id);
			}
		}

		/// <inheritdoc />
		public void Stop()
		{
			if (this.CheckInit("Stop"))
			{
				this.port.PressAndRelease("STOP", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void Yellow()
		{
			if (this.CheckInit("Yellow"))
			{
				this.port.PressAndRelease("YELLOW", PULSE_TIME);
			}
		}

		/// <inheritdoc />
		public void Select()
		{
			if (this.CheckInit("Select"))
			{
				this.port.PressAndRelease("SELECT", PULSE_TIME);
			}
		}

		private void LoadIrFile()
		{
			if (!this.TryCreateDriverFromResource())
			{
				return;
			}

			string driver = Path.Combine(Directory.GetApplicationDirectory(), DRIVER_NAME);
			Logger.Debug("Loading file {0}...", driver);
			if (string.IsNullOrEmpty(driver))
			{
				Logger.Error("DirecTvCableBoxIr.LoadIrFile() - Cannot find driver resource.");
				return;
			}

			this.port.LoadIRDriver(driver);
		}

		private bool CheckInit(string methodName)
		{
			if (!this.IsInitialized)
			{
				Logger.Error("DirecTvCableBoxIr.{0}() - Device not initialized.", methodName);
				return false;
			}

			return true;
		}

		private bool TryCreateDriverFromResource()
		{
			try
			{
				// Get the assembly containing the embedded resource
				Assembly assembly = Assembly.GetExecutingAssembly();

				// Read the embedded resource stream
				string resourceName = string.Format("DirecTvCableBox.{0}", DRIVER_NAME);
				using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
				{
					if (resourceStream == null)
					{
						throw new Exception("Embedded resource not found.");
					}

					// Create the output file in the executing directory
					string outputFile = Path.Combine(Directory.GetApplicationDirectory(), Path.Combine(Directory.GetApplicationDirectory(), DRIVER_NAME));

					using (FileStream fileStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
					{
						// Copy the contents from the resource stream to the file stream
						byte[] buffer = new byte[1024];
						int bytesRead;
						while ((bytesRead = resourceStream.Read(buffer, 0, buffer.Length)) > 0)
						{
							fileStream.Write(buffer, 0, bytesRead);
						}
					}
				}

				return true;
			}
			catch (Exception e)
			{
				Logger.Error(e, "DirectTvCableBoxIr.TryCreateDriverFromResource()");
				return false;
			}
		}
	}
}
