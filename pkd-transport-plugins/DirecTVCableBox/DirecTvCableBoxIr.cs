using System.Reflection;

namespace DirecTvCableBox
{
	using Crestron.SimplSharp.CrestronIO;
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
		private const string DriverName = "CableBox_DirecTV_H25-100.ir";
		private const int PulseTime = 150;
		private IROutputPort? _port;

		/// <summary>
		/// Instantiates a new instance of <see cref="DirecTvCableBoxIr"/>.
		/// </summary>
		public DirecTvCableBoxIr()
		{
			SupportsColorButtons = true;
			SupportsDiscretePower = true;
		}

		/// <inheritdoc />
		public event EventHandler<GenericSingleEventArgs<string>>? ConnectionChanged;

		/// <inheritdoc />
		public string Id { get; private set; } = string.Empty;

		/// <inheritdoc />
		public bool IsInitialized { get; private set; }

		/// <inheritdoc />
		public bool IsOnline { get; private set; }

		/// <inheritdoc />
		public string Label { get; private set; } = string.Empty;

		/// <inheritdoc />
		public bool SupportsColorButtons { get; }

		/// <inheritdoc />
		public bool SupportsDiscretePower { get; }

		/// <inheritdoc />
		public void Initialize(IROutputPort port, string id, string label)
		{
			ParameterValidator.ThrowIfNull(port, "DirecTvCableBoxIr.Ctor", nameof(port));
			ParameterValidator.ThrowIfNullOrEmpty(id, "DirecTvCableBoxIr.Ctor", nameof(id));
			ParameterValidator.ThrowIfNullOrEmpty(label, "DirecTvCableBoxIr.Ctor", nameof(label));

			Id = id;
			Label = label;
			_port = port;
			LoadIrFile();
			IsInitialized = true;
			IsOnline = true;
			var temp = ConnectionChanged;
			temp?.Invoke(this, new GenericSingleEventArgs<string>(Id));
		}

		/// <inheritdoc />
		public void Connect() { }

		/// <inheritdoc />
		public void Disconnect() { }

		/// <inheritdoc />
		public void Back()
		{
			if (CheckInit("Back"))
			{
				_port?.PressAndRelease("BACK", PulseTime);
			}
		}

		/// <inheritdoc />
		public void Blue()
		{
			if (CheckInit("Blue"))
			{
				_port?.PressAndRelease("BLUE", PulseTime);
			}
		}

		/// <inheritdoc />
		public void ChannelDown()
		{
			if (CheckInit("ChannelDown"))
			{
				_port?.PressAndRelease("CH-", PulseTime);
			}
		}

		/// <inheritdoc />
		public void ChannelUp()
		{
			if (CheckInit("ChannelUp"))
			{
				_port?.PressAndRelease("CH+", PulseTime);
			}
		}

		/// <inheritdoc />
		public void Dash()
		{
			if (CheckInit("Dash"))
			{
				_port?.PressAndRelease("DIGIT_-", PulseTime);
			}
		}

		/// <inheritdoc />
		public void Digit(ushort digit)
		{
			if (!CheckInit("Digit"))
			{
				return;
			}

			_port?.PressAndRelease(digit.ToString(), PulseTime);
		}

		/// <inheritdoc />
		public void Exit()
		{
			if (CheckInit("Exit"))
			{
				_port?.PressAndRelease("EXIT", PulseTime);
			}
		}

		/// <inheritdoc />
		public void Green()
		{
			if (CheckInit("Green"))
			{
				_port?.PressAndRelease("GREEN", PulseTime);
			}
		}

		/// <inheritdoc />
		public void Guide()
		{
			if (CheckInit("Guide"))
			{
				_port?.PressAndRelease("GUIDE", PulseTime);
			}
		}

		/// <inheritdoc />
		public void Info()
		{
			if (CheckInit("Info"))
			{
				_port?.PressAndRelease("INFO", PulseTime);
			}
		}

		/// <inheritdoc />
		public void Menu()
		{
			if (CheckInit("Menu"))
			{
				_port?.PressAndRelease("MENU", PulseTime);
			}
		}

		/// <inheritdoc />
		public void NavDown()
		{
			if (CheckInit("NavDown"))
			{
				_port?.PressAndRelease("DN_ARROW", PulseTime);
			}
		}

		/// <inheritdoc />
		public void NavLeft()
		{
			if (CheckInit("NavLeft"))
			{
				_port?.PressAndRelease("LEFT_ARROW", PulseTime);
			}
		}

		/// <inheritdoc />
		public void NavRight()
		{
			if (CheckInit("NavRight"))
			{
				_port?.PressAndRelease("RIGHT_ARROW", PulseTime);
			}
		}

		/// <inheritdoc />
		public void NavUp()
		{
			if (CheckInit("NavUp"))
			{
				_port?.PressAndRelease("UP_ARROW", PulseTime);
			}
		}

		/// <inheritdoc />
		public void PageDown()
		{
			if (CheckInit("PageDown"))
			{
				_port?.PressAndRelease("PAGE_DOWN", PulseTime);
			}
		}

		/// <inheritdoc />
		public void PageUp()
		{
			if (CheckInit("PageUp"))
			{
				_port?.PressAndRelease("PAGE_UP", PulseTime);
			}
		}

		/// <inheritdoc />
		public void Pause()
		{
			if (CheckInit("Pause"))
			{
				_port?.PressAndRelease("PAUSE", PulseTime);
			}
		}

		/// <inheritdoc />
		public void Play()
		{
			if (CheckInit("Play"))
			{
				_port?.PressAndRelease("PLAY", PulseTime);
			}
		}

		/// <inheritdoc />
		public void PowerOff()
		{
			if (CheckInit("PowerOff"))
			{
				_port?.PressAndRelease("POWER_OFF", PulseTime);
			}
		}

		/// <inheritdoc />
		public void PowerOn()
		{
			if (CheckInit("PowerOn"))
			{
				_port?.PressAndRelease("POWER_ON", PulseTime);
			}
		}

		/// <inheritdoc />
		public void PowerToggle()
		{
			if (CheckInit("PowerTOggle"))
			{
				Logger.Warn("DirecTvCableBoxIr {0} - PowerToggle() is not supported by this device.", Id);
			}
		}

		/// <inheritdoc />
		public void Record()
		{
			if (CheckInit("Record"))
			{
				_port?.PressAndRelease("RECORD", PulseTime);
			}
		}

		/// <inheritdoc />
		public void Red()
		{
			if (CheckInit("Red"))
			{
				_port?.PressAndRelease("RED", PulseTime);
			}
		}

		/// <inheritdoc />
		public void ScanForward()
		{
			if (CheckInit("ScanForward()"))
			{
				_port?.PressAndRelease("FSCAN", PulseTime);
			}
		}

		/// <inheritdoc />
		public void ScanReverse()
		{
			if (CheckInit("ScanReverse"))
			{
				_port?.PressAndRelease("RSCAN", PulseTime);
			}
		}

		/// <inheritdoc />
		public void SkipForward()
		{
			if (CheckInit("SkipForward"))
			{
				Logger.Warn("DirecTvCableBoxIr {0} - SkipForward() is not supported by this device.", Id);
			}
		}

		/// <inheritdoc />
		public void SkipReverse()
		{
			if (CheckInit("SkipForward"))
			{
				Logger.Warn("DirecTvCableBoxIr {0} - SkipReverse() is not supported by this device.", Id);
			}
		}

		/// <inheritdoc />
		public void Stop()
		{
			if (CheckInit("Stop"))
			{
				_port?.PressAndRelease("STOP", PulseTime);
			}
		}

		/// <inheritdoc />
		public void Yellow()
		{
			if (CheckInit("Yellow"))
			{
				_port?.PressAndRelease("YELLOW", PulseTime);
			}
		}

		/// <inheritdoc />
		public void Select()
		{
			if (CheckInit("Select"))
			{
				_port?.PressAndRelease("SELECT", PulseTime);
			}
		}

		private void LoadIrFile()
		{
			if (!TryCreateDriverFromResource())
			{
				return;
			}

			string driver = Path.Combine(Directory.GetApplicationDirectory(), DriverName);
			Logger.Debug("Loading file {0}...", driver);
			if (string.IsNullOrEmpty(driver))
			{
				Logger.Error("DirecTvCableBoxIr.LoadIrFile() - Cannot find driver resource.");
				return;
			}

			_port?.LoadIRDriver(driver);
		}

		private bool CheckInit(string methodName)
		{
			if (!IsInitialized)
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
				var assembly = Assembly.GetExecutingAssembly();

				// Read the embedded resource stream
				const string resourceName = $"DirecTVCableBox.{DriverName}";
				using var resourceStream = assembly.GetManifestResourceStream(resourceName);
				if (resourceStream == null)
				{
					throw new Exception("Embedded resource not found.");
				}

				// Create the output file in the executing directory
				string outputFile = Path.Combine(
					Directory.GetApplicationDirectory(),
					Path.Combine(Directory.GetApplicationDirectory(),DriverName));

				using var fileStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
				// Copy the contents from the resource stream to the file stream
				var buffer = new byte[1024];
				int bytesRead;
				while ((bytesRead = resourceStream.Read(buffer, 0, buffer.Length)) > 0)
				{
					fileStream.Write(buffer, 0, bytesRead);
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
