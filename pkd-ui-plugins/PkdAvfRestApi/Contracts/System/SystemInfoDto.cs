namespace PkdAvfRestApi.Contracts.System;

internal record SystemInfoDto(
    string Id,
    string Label,
    string Manufacturer,
    string Model,
    string HelpContact,
    string SystemType,
    List<string> Tags
);