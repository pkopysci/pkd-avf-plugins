namespace EventsTestingAppService;

using pkd_application_service.CustomEvents;
using pkd_common_utils.Logging;

public class EventsTestingApplication : CustomEventAppService
{
	private const string TestEventId = "event01";
	private const string TestEventId2 = "event02";
	private const string TestEventId3 = "event03";

	public EventsTestingApplication()
	{
		events.Add(TestEventId, new CustomEventInfoContainer(
			TestEventId,
			"Test Custom Event",
			string.Empty,
			[]
		));
		
		events.Add(TestEventId2, new CustomEventInfoContainer(
			TestEventId2,
			"Test Custom Event 2",
			string.Empty,
			[]
		));

		events.Add(TestEventId3, new CustomEventInfoContainer(
			TestEventId3,
			"Test Custom Event 3",
			string.Empty,
			[]
		));

		customEvents.Add(TestEventId, HandleEvent1ChangeRequest);
		customEvents.Add(TestEventId2, HandleEvent2ChangeRequest);
		customEvents.Add(TestEventId3, HandleEvent3ChangeRequest);

	}

	/// <summary>
	/// Do any special things that should happen before the rest of the system changes active/standby status.
	/// </summary>
	protected override void OnSystemChange()
	{
		Logger.Debug("EventTestingApplication.OnSystemChange()");

		events[TestEventId].IsActive = false;
		events[TestEventId2].IsActive = false;
		events[TestEventId3].IsActive = false;
		NotifyStateChange(TestEventId);
		NotifyStateChange(TestEventId2);
		NotifyStateChange(TestEventId3);

		base.OnSystemChange();
	}

	private void HandleEvent1ChangeRequest(bool newState)
	{
		// Typically we would recall DSP settings, set any specific routing,
		// and any other non-standard logic that is required by the event.

		Logger.Debug("EventTestingApplication.HandleEvent1ChangeRequest({0})", newState);

		events[TestEventId].IsActive = newState;
		NotifyStateChange(TestEventId);
	}

	private void HandleEvent2ChangeRequest(bool newState)
	{
		// Typically we would recall DSP settings, set any specific routing,
		// and any other non-standard logic that is required by the event.

		Logger.Debug("EventTestingApplication.HandleEvent2ChangeRequest({0})", newState);

		events[TestEventId2].IsActive = newState;
		NotifyStateChange(TestEventId2);
	}

	private void HandleEvent3ChangeRequest(bool newState)
	{
		// Typically we would recall DSP settings, set any specific routing,
		// and any other non-standard logic that is required by the event.

		Logger.Debug("EventTestingApplication.HandleEvent3ChangeRequest({0})", newState);

		events[TestEventId3].IsActive = newState;
		NotifyStateChange(TestEventId3);
	}
}