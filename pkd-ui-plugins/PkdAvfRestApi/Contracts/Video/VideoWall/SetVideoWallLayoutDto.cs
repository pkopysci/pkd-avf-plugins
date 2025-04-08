using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Video.VideoWall;

internal record SetVideoWallLayoutDto(
    [Required] string VideoWall,
    [Required] string Layout
);
