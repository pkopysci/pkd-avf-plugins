using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Cameras;

internal record SetCameraPresetDto(
    [Required] string Preset
);