namespace AvProEdgeAvSwitch
{
	using Crestron.SimplSharp.CrestronIO;
	using pkd_common_utils.FileOps;
	using pkd_common_utils.Logging;
	using Newtonsoft.Json;
	using System;
	using System.Linq;
	using System.Text;

	internal static class MxnetConfigReader
	{
		public static MxnetConfig? TryReadConfig(string deviceId)
		{
			try
			{
				var userDir = DirectoryHelper.GetUserFolder();
				var configFormat = $"mxnet_config_device_id_{deviceId}.json";
				var path = Directory
					.GetFiles(userDir, configFormat)
					.FirstOrDefault(file => file.EndsWith(".json"));

				if (string.IsNullOrEmpty(path))
				{
					Logger.Error("MxnetConfigReader.TryReadConfig({0}) - no file found with matching id.", deviceId);
					return null;
				}

				string data = ReadAllLines(path);
				if (string.IsNullOrEmpty(data))
				{
					Logger.Error("MxnetConfigReader.TryReadConfig({0}) - unable to read data from {1}", deviceId, path);
					return null;
				}

				return JsonConvert.DeserializeObject<MxnetConfig>(data);
			}
			catch (Exception e)
			{
				Logger.Error(e, "MxnetConfigReader.TryReadConfig()");
				return null;
			}
		}

		private static string ReadAllLines(string path)
		{
			try
			{
				var builder = new StringBuilder();
				using (StreamReader reader = new StreamReader(path))
				{
					while (reader.ReadLine() is { } line)
					{
						builder.Append(line);
					}
				}

				return builder.ToString();
			}
			catch (Exception e)
			{
				Logger.Error(e, "MxnetConfigReader.ReadAllLines()");
				return string.Empty;
			}
		}
	}
}
