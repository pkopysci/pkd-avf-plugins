namespace NecMultisyncDisplay;

using pkd_common_utils.Logging;
using System;
using System.Collections.Generic;

/// <summary>
/// Helper class for creating and parsing NEC Multisync display responses
/// </summary>
internal static class MultisyncRxTools
{
	public static readonly byte[] PowerOnCommand = [0x01, 0x30, 0x41, 0x30, 0x41, 0x30, 0x43, 0x02, 0x43, 0x32, 0x30, 0x33, 0x44, 0x36, 0x30, 0x30, 0x30, 0x31, 0x03, 0x73, 0x0d
	];
	public static readonly byte[] PowerOffCommand = [0x01, 0x30, 0x41, 0x30, 0x41, 0x30, 0x43, 0x02, 0x43, 0x32, 0x30, 0x33, 0x44, 0x36, 0x30, 0x30, 0x30, 0x34, 0x03, 0x76, 0x0d
	];
	public static readonly byte[] PowerQueryCommand = [0x01, 0x30, 0x41, 0x30, 0x41, 0x30, 0x36, 0x02, 0x30, 0x31, 0x44, 0x36, 0x03, 0x74, 0x0D
	];
	public static readonly byte[] PowerGetRxHeader = [0x30, 0x32, 0x30, 0x30, 0x44, 0x36, 0x30, 0x30, 0x30, 0x30, 0x30, 0x34, 0x30, 0x30, 0x30
	];
	public static readonly byte[] PowerSetRxHeader = [0x30, 0x30, 0x43, 0x32, 0x30, 0x33, 0x44, 0x36, 0x30, 0x30, 0x30
	];
	public const byte PowerOnByte = 0x31;
	public const byte PowerOffByte = 0x34;
	public const byte PowerStandbyByte = 0x32;

	/// <summary>
	/// Command to query the currently selected input on the display.
	/// </summary>
	public static readonly byte[] InputQueryCommand = [0x01, 0x30, 0x41, 0x30, 0x43, 0x30, 0x36, 0x02, 0x30, 0x30, 0x36, 0x30, 0x03, 0x03, 0x0D
	];

	/// <summary>
	/// Collection of commands for sending input selection changes.
	/// 0 - empty array,
	/// 1 - HDMI 1,
	/// 2 - HDMI 2,
	/// 3 - Display Port,
	/// </summary>
	public static readonly List<byte[]> InputSelectCommands =
	[
        Array.Empty<byte>(),
		new byte[]
		{
			0x01, 0x30, 0x41, 0x30, 0x45, 0x30, 0x41, 0x02, 0x30, 0x30, 0x36, 0x30, 0x30, 0x30, 0x31, 0x31, 0x03, 0x72,
			0x0D
		},
		new byte[]
		{
			0x01, 0x30, 0x41, 0x30, 0x45, 0x30, 0x41, 0x02, 0x30, 0x30, 0x36, 0x30, 0x30, 0x30, 0x31, 0x32, 0x03, 0x71,
			0x0D
		},
		new byte[]
		{
			0x01, 0x30, 0x41, 0x30, 0x45, 0x30, 0x41, 0x02, 0x30, 0x30, 0x36, 0x30, 0x30, 0x30, 0x30, 0x46, 0x03, 0x04,
			0x0D
		}
	];

	public static readonly Dictionary<InputRxTypes, byte[]> InputRxData = new Dictionary<InputRxTypes, byte[]>()
	{
		{ InputRxTypes.QueryHdmi1, [0x31, 0x31, 0x03, 0x01] },
		{ InputRxTypes.QueryHdmi2, [0x31, 0x32, 0x03, 0x02] },
		{ InputRxTypes.QueryDp, [0x30, 0x46, 0x03, 0x77] },
		{ InputRxTypes.AckHdmi1, [0x31, 0x31, 0x03, 0x03] },
		{ InputRxTypes.AckHdmi2, [0x31, 0x32, 0x03, 0x00] },
		{ InputRxTypes.AckDp, [0x30, 0x46, 0x03, 0x75] },
	};

	/// <summary>
	/// Compare 2 byte arrays for equivalence. they will be considered equivalent if each byte in
	/// array1 matches the byte in the same position of array2.
	/// </summary>
	/// <param name="array1">The first array to use in the comparison.</param>
	/// <param name="array2">The second array to use in the comparison.</param>
	/// <returns>True if a match is found, false otherwise.</returns>
	public static bool CompareByteArrays(byte[] array1, byte[] array2)
	{
		if (array1.Length != array2.Length)
		{
			return false;
		}

		for (int i = 0; i < array1.Length; i++)
		{
			byte result = (byte)(array1[i] & array2[i]);

			if (result != array1[i])
			{
				return false;
			}
		}

		return true;
	}

	public static byte[] GetCommand(byte[] data)
	{
		if (data.Length < 11)
		{
			return [];
		}

		var command = new byte[data.Length - 11];

		try
		{
			Array.Copy(data, 8, command, 0, data.Length - 11);
			return command;
		}
		catch (Exception e)
		{
			Logger.Error("MultisyncRxTools.GetCommand({0}) - failed to parse command response.", data, e.Message);
			return [];
		}
	}
}

internal enum InputRxTypes
{
	QueryHdmi1,
	QueryHdmi2,
	QueryDp,
	AckHdmi1,
	AckHdmi2,
	AckDp
}