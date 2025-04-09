using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Cameras;

internal record SetCameraPowerDto(
    [Required] bool State
);