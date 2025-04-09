using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Cameras;

internal record SetCameraZoomDto(
    [Required] [Range(0,100)] int Speed
);