using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Video.Displays;

public record UpdateScreenDto([Required] bool Position);