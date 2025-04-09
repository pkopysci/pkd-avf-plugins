namespace PkdAvfRestApi.Contracts.Lighting;

internal record LightingSceneDto(
    string Id,
    string Label,
    string Icon,
    int Index,
    bool IsActive,
    List<string> Tags
);