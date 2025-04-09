namespace PkdAvfRestApi.Contracts.Cameras;

internal record CameraDto(
    string Id,
    string Label,
    string Manufacturer,
    string Model,
    string Icon,
    bool IsOnline,
    bool SupportsSavingPresets,
    bool SupportsZoom,
    bool SupportsPanTilt,
    bool SupportsPower,
    bool PowerState,
    List<string> Tags,
    List<CameraPresetDto> Presets
);