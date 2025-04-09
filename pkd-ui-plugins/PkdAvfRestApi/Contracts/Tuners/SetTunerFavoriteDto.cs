using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Tuners;

internal record SetTunerFavoriteDto(
    [Required] string Id
);