using pkd_application_service.AudioControl;
using pkd_application_service.CameraControl;

namespace NursingAppService.DataObjects;

public record NursingAreaDto(
    string Id,
    string Label,
    List<CameraInfoContainer> Cameras,
    List<AudioChannelInfoContainer> Microphones,
    List<AudioChannelInfoContainer> Outputs
);