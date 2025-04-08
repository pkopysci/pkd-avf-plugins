namespace PkdAvfRestApi.Contracts.System;

internal record MenuItemDto(
    string Id,
    string Label,
    string Icon,
    string Control,
    List<string> Tags
);

internal record UserInterfaceDto(
    string Id,
    string Label,
    string Manufacturer,
    string Model,
    List<string> Tags,
    bool IsOnline,
    int IpId,
    List<MenuItemDto> MenuItems
);