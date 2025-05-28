using System.Text;
using Newtonsoft.Json;
using pkd_common_utils.Logging;
using pkd_domain_service.Data.RoutingData;
using pkd_hardware_service.VideoWallDevices;

namespace VideoWallEmulation;

internal class ConfigData
{
    public List<Source> Sources { get; init; } = [];
    public List<EmulatedCanvas> Canvases { get; init; } = [];
}

internal static class WallData
{
    
    public static bool TryReadConfig(string filePath, out ConfigData configData)
    {
        try
        {
            var builder = new StringBuilder();
            using StreamReader reader = new(filePath);
            while (reader.ReadLine() is { } line)
            {
                builder.AppendLine(line);
            }

            var rawData = builder.ToString();
            if (string.IsNullOrEmpty(rawData))
            {
                Logger.Error($"VideoWallEmulator - Config file {filePath} does not contain config information.");
                configData = new ConfigData();
                return false;
            }

            var readData = JsonConvert.DeserializeObject<ConfigData>(builder.ToString());
            if (readData == null)
            {
                Logger.Error($"VideoWallEmulator - failed to deserialize data contain in {filePath}.");
                configData = new ConfigData();
                return false;
            }

            configData = readData;
            return true;
        }
        catch (Exception exception)
        {
            Logger.Error(exception, $"VideoWallEmulator.TryReadConfig({filePath})");
            configData = new ConfigData();
            return false;
        }
    }
}