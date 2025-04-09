using System.ComponentModel.DataAnnotations;

namespace PkdAvfRestApi.Contracts.Lighting;

internal record SetLightingSceneDto([Required] string SceneId);