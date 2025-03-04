namespace BiampNexiaDsp
{
	using pkd_common_utils.Logging;
	using System;
	using System.Collections.Generic;

	internal enum NexiaCommands
	{
		Unknown,
		Inc,
		IncD,
		Dec,
		DecD,
		Set,
		SetL,
		SetD,
		Get,
		GetL,
		GetD,
		Recall
	}

	internal enum NexiaBlocks
	{
		Unknown,
		FaderLevel,
		FaderMute,
		Preset
	}

	/// <summary>
	/// Helper class for building control or query commands for a Biamp Nexia device.
	/// </summary>
	internal static class NexiaCommander
	{
		//private static readonly int lvlMin = -100;
		//private static readonly int lvlMax = 12;

		/// <summary>
		/// Collection of supported device control types used when sending changes or querying state.
		/// </summary>
		public static readonly Dictionary<NexiaBlocks, string> Blocks = new Dictionary<NexiaBlocks, string>()
		{
			{ NexiaBlocks.FaderLevel, "FDRLVL" },
			{ NexiaBlocks.FaderMute, "FDRMUTE" },
			{ NexiaBlocks.Preset, "PRESET" }
		};

		/// <summary>
		/// Collection of supported commands used when sending changes or querying state.
		/// </summary>
		public static readonly Dictionary<NexiaCommands, string> Commands = new Dictionary<NexiaCommands, string>()
		{
			{ NexiaCommands.Inc, "INC" },
			{ NexiaCommands.IncD, "INCD" },
			{ NexiaCommands.DecD, "DECD" },
			{ NexiaCommands.Dec, "DEC" },
			{ NexiaCommands.Set, "SET" },
			{ NexiaCommands.SetL, "SETL" },
			{ NexiaCommands.SetD, "SETD"},
			{ NexiaCommands.Get, "GET"},
			{ NexiaCommands.GetL, "GETL" },
			{ NexiaCommands.GetD, "GETD" },
			{ NexiaCommands.Recall, "RECALL" }
		};

		/// <summary>
		/// converts a value in the range 0-100 to -100-12.
		/// if percent is less than 0 it will be set to 0. if percent > 100 it will be set to 100.
		/// </summary>
		/// <param name="percent">the 0-100 value to convert.</param>
		/// <param name="lvlMin">the minimum value in the Db range.</param>
		/// <param name="lvlMax">The maximum value in the Db range.</param>
		/// <returns>a new value in the range -100 to 12 relative to the percent.</returns>
		public static float ConvertToDb(int percent, int lvlMin, int lvlMax)
		{
			int rawVal;
			if (percent < 0)
			{
				rawVal = 0;
			}
			else if (percent > 100)
			{
				rawVal = 100;
			}
			else
			{
				rawVal = percent;
			}

			float newRange = lvlMax - lvlMin;
			return ((rawVal * newRange) / 100) + lvlMin;
		}

		/// <summary>
		/// converts a value in the range-100-12 to 0-100.
		/// if dbLevel is less than the defined minimum then it will set be to the minimum.
		/// if dbLevel is greater than the defined maximum then it will be set to the maximum.
		/// </summary>
		/// <param name="dbLevel">dB value that will be converted to a percentage.</param>
		/// <param name="lvlMin">The minimum allowed dB value in the range.</param>
		/// <param name="lvlMax">The maximum allowed dB value in the range.</param>
		/// <returns>a new value representing the 0-100 percent of the provided level in the given range.</returns>
		public static int ConvertToPercent(int dbLevel, int lvlMin, int lvlMax)
		{
			int rawVal;
			if (dbLevel < lvlMin)
			{
				rawVal = lvlMin;
			}
			else if (dbLevel > lvlMax)
			{
				rawVal = lvlMax;
			}
			else
			{
				rawVal = dbLevel;
			}

			const int newRange = 100;
			return (rawVal - lvlMin) * newRange / (lvlMax - lvlMin);
		}

		/// <summary>
		/// converts a value in the range-100-12 to 0-100.
		/// if dvLevel is less than -100 it will be set to -100. if percent > 12 it will be set to 12.
		/// </summary>
		/// <param name="dbLevel">the 0-100 value to convert.</param>
		/// <param name="lvlMin">The minimum allowed dB value in the range.</param>
		/// <param name="lvlMax">The maximum allowed dB value in the range.</param> 
		/// <returns>a new value in the range -100 to 12 relative to the percent.</returns>
		public static int FloatToPercent(float dbLevel, int lvlMin, int lvlMax)
		{
			Logger.Debug("NexiaCommander.FloatToPercent({0}, {1}, {2})", dbLevel, lvlMin, lvlMax);

			double rawVal;
			if (dbLevel < lvlMin)
			{
				rawVal = lvlMin;
			}
			else if (dbLevel > lvlMax)
			{
				rawVal = lvlMax;
			}
			else
			{
				rawVal = dbLevel;
			}

			rawVal = Math.Round(rawVal, 0);

			Logger.Debug("NexiaCommander.FloatToPercent() - rawVal after rounding: {0}", rawVal);
			const int newRange = 100;
			return (int)(rawVal - lvlMin) * newRange / (lvlMax - lvlMin);
		}
	}
}
