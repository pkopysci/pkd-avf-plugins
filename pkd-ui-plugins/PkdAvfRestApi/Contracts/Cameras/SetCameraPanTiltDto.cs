using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Cameras;

internal record SetCameraPanTiltDto(
    [Required] [Range(0,100)] int X,
    [Required] [Range(0,100)] int Y
);