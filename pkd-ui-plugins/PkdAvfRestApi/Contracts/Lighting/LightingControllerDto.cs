namespace PkdAvfRestApi.Contracts.Lighting;

internal record LightingControllerDto(
    string Id,
    string Label,
    string Manufacturer,
    string Model,
    string Icon,
    bool IsOnline,
    List<string> Tags,
    List<LightingZoneDto> Zones,
    List<LightingSceneDto> Scenes
);