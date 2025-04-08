namespace PkdAvfRestApi.Contracts.Lighting;

internal record LightingZoneDto(
    string Id,
    string Label,
    string Icon,
    int Index,
    int Load,
    List<string> Tags
);