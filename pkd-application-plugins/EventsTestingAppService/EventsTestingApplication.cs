namespace EventsTestingAppService
{
	using pkd_application_service.CustomEvents;
	using pkd_common_utils.Logging;
	using System.Collections.Generic;

	public class EventsTestingApplication : CustomEventAppService
	{
		private static readonly string TEST_EVENT_ID = "event01";
		private static readonly string TEST_EVENT_ID2 = "event02";
		private static readonly string TEST_EVENT_ID3 = "event03";

		public EventsTestingApplication()
		: base()
		{
			events.Add(TEST_EVENT_ID, new CustomEventInfoContainer(
				TEST_EVENT_ID,
				"Test Custom Event",
				string.Empty,
				new List<string>()
				));
			
			events.Add(TEST_EVENT_ID2, new CustomEventInfoContainer(
				TEST_EVENT_ID2,
				"Test Custom Event 2",
				string.Empty,
				new List<string>()
				));

			events.Add(TEST_EVENT_ID3, new CustomEventInfoContainer(
				TEST_EVENT_ID3,
				"Test Custom Event 3",
				string.Empty,
				new List<string>()
				));

			customEvents.Add(TEST_EVENT_ID, HandleEvent1ChangeRequest);
			customEvents.Add(TEST_EVENT_ID2, HandleEvent2ChangeRequest);
			customEvents.Add(TEST_EVENT_ID3, HandleEvent3ChangeRequest);

		}

		/// <summary>
		/// Do any special things that should happen before the rest of the system changes active/standby status.
		/// </summary>
		protected override void OnSystemChange()
		{
			Logger.Debug("EventTestingApplication.OnSystemChange()");

			events[TEST_EVENT_ID].IsActive = false;
			events[TEST_EVENT_ID2].IsActive = false;
			events[TEST_EVENT_ID3].IsActive = false;
			NotifyStateChange(TEST_EVENT_ID);
			NotifyStateChange(TEST_EVENT_ID2);
			NotifyStateChange(TEST_EVENT_ID3);

			base.OnSystemChange();
		}

		private void HandleEvent1ChangeRequest(bool newState)
		{
			// Typically we would recall DSP settings, set any specific routing,
			// and any other non-standard logic that is required by the event.

			Logger.Debug("EventTestingApplication.HandlEvent1ChangeRequest({0})", newState);

			events[TEST_EVENT_ID].IsActive = newState;
			NotifyStateChange(TEST_EVENT_ID);
		}

		private void HandleEvent2ChangeRequest(bool newState)
		{
			// Typically we would recall DSP settings, set any specific routing,
			// and any other non-standard logic that is required by the event.

			Logger.Debug("EventTestingApplication.HandlEvent2ChangeRequest({0})", newState);

			events[TEST_EVENT_ID2].IsActive = newState;
			NotifyStateChange(TEST_EVENT_ID2);
		}

		private void HandleEvent3ChangeRequest(bool newState)
		{
			// Typically we would recall DSP settings, set any specific routing,
			// and any other non-standard logic that is required by the event.

			Logger.Debug("EventTestingApplication.HandlEvent3ChangeRequest({0})", newState);

			events[TEST_EVENT_ID3].IsActive = newState;
			NotifyStateChange(TEST_EVENT_ID3);
		}
	}
}
