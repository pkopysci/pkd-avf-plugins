using pkd_application_service.AudioControl;
using pkd_application_service.AvRouting;
using pkd_application_service.Base;
using pkd_application_service.CameraControl;
using pkd_application_service.DisplayControl;

namespace NursingAppService.DataObjects;

public record DebriefNursingAreaDto (
    string Id,
    string Label,
    List<CameraInfoContainer> Cameras,
    List<AudioChannelInfoContainer> Microphones,
    List<AudioChannelInfoContainer> Outputs,
    List<DisplayInfoContainer> Displays,
    List<AvSourceInfoContainer> AvInputs,
    List<InfoContainer> VideoOutputs
    );