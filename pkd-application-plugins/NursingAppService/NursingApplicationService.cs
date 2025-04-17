using NursingAppService.DataObjects;
using pkd_application_service;
using pkd_common_utils.GenericEventArgs;
using pkd_domain_service;
using pkd_hardware_service;

namespace NursingAppService;

public class NursingApplicationService : ApplicationService
{
    /// <summary>
    /// args.Arg = id of the control station that changed.
    /// </summary>
    public event EventHandler<GenericSingleEventArgs<string>>? NursingScenarioChanged;

    /// <returns>A collection of all NursingArea rooms in the configuration.</returns>
    public List<NursingAreaDto> GetAllLabs()
    {
        throw new NotImplementedException();
    }

    /// <returns>A collection of all Debrief rooms in the configuration.</returns>
    public List<DebriefNursingAreaDto> GetAllDebriefRooms()
    {
        throw new NotImplementedException();
    }

    /// <returns>A collection of all control stations in the configuration.</returns>
    public List<NursingControlStation> GetAllNursingControlStations()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Search through the collection of nursing stations and find the first one associated with the user interface
    /// id.
    /// </summary>
    /// <param name="interfaceId">The id of the user interface that is assigned to the target station.</param>
    /// <param name="station">object reference that will be assigned if a station is found.</param>
    /// <returns>true if a valid nursing station was found, false otherwise.</returns>
    public bool TryGetNursingControlStation(string interfaceId, out NursingControlStation? station)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Trigger the routing, system state, and recording settings associated with the scenario id for the target control
    /// station.
    /// </summary>
    /// <param name="stationId">The id of the station being changed.</param>
    /// <param name="scenarioId">The id of the scenario settings to recall.</param>
    public void RecallNursingScenario(string stationId, string scenarioId)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Query the currently selected scenario for the target control station.
    /// </summary>
    /// <param name="stationId">The id of the control station to query.</param>
    /// <returns>the id of the active scenario or an empty string if none is select.</returns>
    public string GetActiveNursingScenarioId(string stationId)
    {
        throw new NotImplementedException();
    }

    public override void Initialize(IInfrastructureService hwService, IDomainService domain)
    {
        // TODO: Create nursing objects from hardware and domain config
        base.Initialize(hwService, domain);
    }

    public override void SetStandby()
    {
        // TODO: clear all mic recording enables, recall DSP presets, clear are recording video
        base.SetStandby();
    }

    public override void SetActive()
    {
        base.SetActive();
        // TODO: set to a default scenario and windower
    }
}