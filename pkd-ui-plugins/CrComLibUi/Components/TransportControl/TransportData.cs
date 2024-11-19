namespace CrComLibUi.Components.TransportControl
{
	using pkd_ui_service.Utility;
	using System.Collections.Generic;

	internal class Favorite
	{
		public string Id { get; set; } = string.Empty;
		public string Label { get; set; } = string.Empty;
	}

	internal class TransportData
	{
		public string Id { get; set; } = string.Empty;
		public string Label { get; set; } = string.Empty;
		public string Icon { get; set; } = string.Empty;
		public bool SupportsOnOff { get; set; }
		public bool SupportsColor { get; set; }
		public bool SupportsVideo { get; set; }
		public bool SupportsDvr { get; set; }
		public List<Favorite> Favorites { get; set; } = new List<Favorite>();
		public List<string> Tags { get; set; } = new List<string>();
	}

	internal static class TransportUtilities
	{
		private static Dictionary<string, TransportTypes> Transports = new Dictionary<string, TransportTypes>()
		{
			{ "POWERON", TransportTypes.PowerOn },
			{ "POWEROFF", TransportTypes.PowerOff },
			{ "POWERTOGGLE", TransportTypes.PowerToggle },
			{ "DIAL", TransportTypes.Dial },
			{ "DASH", TransportTypes.Dash },
			{ "CHANUP", TransportTypes.ChannelUp },
			{ "CHANDOWN", TransportTypes.ChannelDown },
			{ "CHANSTOP", TransportTypes.ChannelStop },
			{ "PAGEUP", TransportTypes.PageUp },
			{ "PAGEDOWN", TransportTypes.PageDown },
			{ "PAGESTOP", TransportTypes.PageStop },
			{ "GUIDE", TransportTypes.Guide },
			{ "MENU", TransportTypes.Menu },
			{ "INFO", TransportTypes.Info },
			{ "EXIT", TransportTypes.Exit },
			{ "BACK", TransportTypes.Back },
			{ "PLAY", TransportTypes.Play },
			{ "PAUSE", TransportTypes.Pause },
			{ "STOP", TransportTypes.Stop },
			{ "RECORD", TransportTypes.Record },
			{ "FWD", TransportTypes.ScanForward },
			{ "REV", TransportTypes.ScanReverse },
			{ "SKIPFWD", TransportTypes.SkipForward },
			{ "SKIPREV", TransportTypes.SkipReverse },
			{ "NAVUP", TransportTypes.NavUp },
			{ "NAVDOWN", TransportTypes.NavDown },
			{ "NAVLEFT", TransportTypes.NavLeft },
			{ "NAVRIGHT", TransportTypes.NavRight },
			{ "NAVSTOP", TransportTypes.NavStop },
			{ "RED", TransportTypes.Red },
			{ "YELLOW", TransportTypes.Yellow },
			{ "GREEN", TransportTypes.Green },
			{ "BLUE", TransportTypes.Blue },
			{ "SELECT", TransportTypes.Select },
			{ "PREV", TransportTypes.Previous },
			{ "REPLAY", TransportTypes.Replay },
			{ "DISHNET", TransportTypes.DishNet },
		};

		public static TransportTypes FindTransport(string data)
		{
			if (Transports.TryGetValue(data, out TransportTypes found))
			{
				return found;
			}
			else
			{
				return TransportTypes.Unknown;
			}
		}
	}
}
