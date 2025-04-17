namespace NursingAppService.DataObjects;

public record NursingScenarioDto(
    string Id,
    string Label,
    string WindowerLayoutId,
    List<string> OperatorMicRouteDestinations,
    List<string> ConductorMicRouteDestinations,
    List<string> AudioInputsToRecord,
    List<NursingVideoRouteDto> VideoRoutes);