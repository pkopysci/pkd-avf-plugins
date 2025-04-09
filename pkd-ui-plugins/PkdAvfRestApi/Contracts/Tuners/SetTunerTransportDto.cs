using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Tuners;

internal record SetTunerTransportDto(
    [Required] string Transport
);