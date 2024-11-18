namespace NvxAvSwitch
{
	using Crestron.SimplSharp.Reflection;
	using Crestron.SimplSharpPro;
	using Crestron.SimplSharpPro.DM.Streaming;
	using pkd_common_utils.Logging;
	using NvxAvSwitch.DataObjects;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Utility class for creating NVX encoders and decoders.
	/// </summary>
	internal static class NvxEndpointFactory
	{
		private static readonly Regex CLASS_TAG_PATTERN = new Regex(@"nvx-(.*)");
		private static readonly Regex IPID_TAG_PATTERN = new Regex(@"ipid-(.*)");
		private static readonly Regex IN_TAG_PATTERN = new Regex(@"IN-(.*)");
		private static readonly Regex OUT_TAG_PATTERN = new Regex(@"OUT-(.*)");

		/// <summary>
		/// Uses reflection to instantiate a Crestron NVX endpoint.
		/// This builder does not register the device.
		/// </summary>
		/// <param name="ipId">The IP-ID used for connecting to the endpoint.</param>
		/// <param name="control">The host control system that will control the endpoint.</param>
		/// <param name="classType">The class name of the NVX device that will be created</param>
		/// <returns>the NVX endpoint control object, or NULL if instantiation failed.</returns>
		public static DmNvxBaseClass CreateNvxBase(uint ipId, CrestronControlSystem control, string classType)
		{
			try
			{
				Assembly asm = Assembly.Load("Crestron.SimplSharpPro.DM");
				ConstructorInfo constructor = null;

				foreach (var type in asm.GetTypes())
				{

					if (type.Name.Equals(classType, StringComparison.InvariantCulture))
					{
						CType[] types = new CType[] { typeof(uint), typeof(CrestronControlSystem) };
						constructor = type.GetConstructor(types);
						break;
					}
				}

				if (constructor == null)
				{
					Logger.Error(
						"NvxEndpointFactory.CreateNvxBase - Cannot find standard constructor for NVX with IP-ID 0x{0:X2}",
						ipId);

					return null;
				}

				return constructor.Invoke(new Object[] { ipId, control }) as DmNvxBaseClass;
			}
			catch (Exception e)
			{
				Logger.Error("Failed to create NVX endpoint at IP-ID 0x{0:X2}: {1}", ipId, e.Message);
				return null;
			}
		}

		/// <summary>
		/// Attempts to parse a collection of input or output config item tags for NVX information.
		/// returned object will have string.empty for any unset string values and zero (0) for any unset uint values.
		/// </summary>
		/// <param name="tags">The collection of tags to parse</param>
		/// <returns>a data object containg all parsed data in their correct values.</returns>
		public static NvxTagData ParseTagData(List<string> tags)
		{
			NvxTagData tagData = new NvxTagData();
			foreach (var tag in tags)
			{
				try
				{
					var match = CLASS_TAG_PATTERN.Match(tag);
					if (match.Success)
					{
						tagData.ClassName = match.Groups[1].Value;
						continue;
					}

					match = IPID_TAG_PATTERN.Match(tag);
					if (match.Success)
					{
						tagData.NvxId = tag;
						tagData.IpId = uint.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
						continue;
					}

					match = IN_TAG_PATTERN.Match(tag);
					if (match.Success)
					{
						tagData.InputPort = uint.Parse(match.Groups[1].Value);
						continue;
					}

					match = OUT_TAG_PATTERN.Match(tag);
					if (match.Success)
					{
						tagData.OutputPort = uint.Parse(match.Groups[1].Value);
						continue;
					}
				}
				catch (Exception e)
				{
					Logger.Error("NvxEndpointFactory.ParseTagData() - Failed to parse tag {0}: {1}", tag, e.Message);
					continue;
				}
			}

			return tagData;
		}
	}
}
