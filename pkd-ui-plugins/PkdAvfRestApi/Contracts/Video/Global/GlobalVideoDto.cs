using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Video.Global;

internal record GlobalVideoDto(bool Blank, bool Freeze);

internal record SetGlobalVideoDto([Required] bool Blank, [Required] bool Freeze);