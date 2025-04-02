using PkdAvfRestApi.Tools;

namespace PkdAvfRestApi.Configuration;

public sealed class HostConfig
{
    public string Environment { get; init; } = null!;
    public string ConfigurationPath { get; init; } = null!;
}