namespace PkdAvfRestApi.Contracts.Video.Displays;

public record DisplayDto(
    string Id,
    string Label,
    string Manufacturer,
    string Model,
    string Icon,
    bool IsOnline,
    bool IsFrozen,
    bool IsBlank,
    bool PowerState,
    bool HasScreen,
    List<string> Tags,
    List<DisplayInputDto> Inputs
    );