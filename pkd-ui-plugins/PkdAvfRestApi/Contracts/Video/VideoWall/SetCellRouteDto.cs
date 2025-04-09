using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Video.VideoWall;

internal record SetCellRouteDto(
    [Required] string VideoWall,
    [Required] string Cell,
    [Required] string Input
);