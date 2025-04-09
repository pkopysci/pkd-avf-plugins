using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Video.Routing;

internal record SetVideoRouteDto(
    [Required] string Input,
    [Required] string Output
);