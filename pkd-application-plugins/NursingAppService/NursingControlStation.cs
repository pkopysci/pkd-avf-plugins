using NursingAppService.DataObjects;
using pkd_application_service.AudioControl;
using pkd_application_service.Base;
using pkd_application_service.UserInterface;

namespace NursingAppService;

public class NursingControlStation
{
    public string Id { get; init; } = string.Empty;
    public string Label  { get; init; } = string.Empty;
    public string VideoWindowControllerId  { get; init; } = string.Empty;
    public UserInterfaceDataContainer? UiData { get; init; }
    public List<AudioChannelInfoContainer> AudioInputs { get; init; } = [];
    public List<AudioChannelInfoContainer> AudioOutputs  { get; init; } = [];
    public List<InfoContainer> VideoOutputs  { get; init; } = [];
    public List<NursingScenarioDto> Scenarios { get; init; } = [];
    public string ActiveScenarioId  { get; set; } = string.Empty;

    public bool TryGetConductorMic(out AudioChannelInfoContainer? mic)
    {
        mic = AudioInputs.FirstOrDefault(x => x.Tags.Contains("conductor"));
        return mic != null;
    }

    public bool TryGetOperatorMic(out AudioChannelInfoContainer? mic)
    {
        mic =  AudioOutputs.FirstOrDefault(x => x.Tags.Contains("operator"));
        return mic != null;
    }

    public bool TryGetAiPhone(out AudioChannelInfoContainer? mic)
    {
        mic = AudioOutputs.FirstOrDefault(x => x.Tags.Contains("aiphone"));
        return mic != null;
    }
}