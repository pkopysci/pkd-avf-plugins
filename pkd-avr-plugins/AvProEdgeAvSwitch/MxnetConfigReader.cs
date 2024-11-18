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
		public static MxnetConfig TryReadConfig(string deviceId)
		{
			try
			{
				var userDir = DirectoryHelper.GetUserFolder();
				string configFormat = string.Format("mxnet_config_device_id_{0}.json", deviceId);
				string path = Directory.GetFiles(userDir, configFormat)
					.Where(file => file.EndsWith(".json")).FirstOrDefault();

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
				StringBuilder bldr = new StringBuilder();
				using (StreamReader reader = new StreamReader(path))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						bldr.Append(line);
					}
				}

				return bldr.ToString();
			}
			catch (Exception e)
			{
				Logger.Error(e, "MxnetConfigReader.ReadAllLines()");
				return string.Empty;
			}
		}
	}
}
