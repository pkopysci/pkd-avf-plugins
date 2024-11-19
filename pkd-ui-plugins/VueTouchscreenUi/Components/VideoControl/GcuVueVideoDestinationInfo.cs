namespace CrComLibUi.Components.VideoControl
{
	using pkd_application_service.Base;
	using System.Collections.Generic;
	
	internal class GcuVueVideoDestinationInfo : InfoContainer
	{
		public GcuVueVideoDestinationInfo(string id, string label, string icon, List<string> tags)
		: base(id, label, icon, tags)
		{
		}

		public string CurrentSourceId { get; set; }
	}
}
