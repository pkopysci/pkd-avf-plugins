namespace CrComLibUi.Components.VideoControl;

using System.Collections.Generic;
using pkd_application_service.Base;
	
internal class VideoDestinationInfo(string id, string label, string icon, List<string> tags)
	: InfoContainer(id, label, icon, tags)
{
	public string CurrentSourceId { get; set; } = string.Empty;
}