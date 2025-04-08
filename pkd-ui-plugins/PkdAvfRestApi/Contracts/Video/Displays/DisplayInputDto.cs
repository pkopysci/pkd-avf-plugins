namespace PkdAvfRestApi.Contracts.Video.Displays;

public record DisplayInputDto(
    string Id,
    string Label,
    int InputNumber,
    List<string> Tags,
    bool Selected
    );