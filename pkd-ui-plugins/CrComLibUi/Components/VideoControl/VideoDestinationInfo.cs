namespace CrComLibUi.Components.VideoControl;

using pkd_application_service.Base;
using System.Collections.Generic;
	
internal class VideoDestinationInfo(string id, string label, string icon, List<string> tags)
	: InfoContainer(id, label, icon, tags)
{
	public string CurrentSourceId { get; set; } = string.Empty;
}