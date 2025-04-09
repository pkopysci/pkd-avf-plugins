using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Endpoints;

public record SetTunerFavoriteDto(
    [Required] string Id,
    [Required] string Label
);