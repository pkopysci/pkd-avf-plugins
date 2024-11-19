namespace CrComLibUi.Components.VideoControl
{
	using pkd_application_service.Base;
	using System.Collections.Generic;
	
	internal class VideoDestinationInfo : InfoContainer
	{
		public VideoDestinationInfo(string id, string label, string icon, List<string> tags)
		: base(id, label, icon, tags)
		{
		}

		public string CurrentSourceId { get; set; }
	}
}
