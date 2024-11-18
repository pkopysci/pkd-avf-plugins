namespace AvProEdgeAvSwitch
{
	using Newtonsoft.Json;
	using System.Collections.Generic;

	// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
	internal class InputEncoder
	{
		[JsonProperty("ONLINE")]
		public string ONLINE { get; set; }

		[JsonProperty("CUSTOM NAME")]
		public string CUSTOMNAME { get; set; }

		[JsonProperty("DESCRIPTION")]
		public string DESCRIPTION { get; set; }

		[JsonProperty("EDID MGMT")]
		public string EDIDMGMT { get; set; }

		[JsonProperty("CHANNEL")]
		public string CHANNEL { get; set; }

		[JsonProperty("IP ADDRESS(AUTO)")]
		public string IPADDRESSAUTO { get; set; }

		[JsonProperty("MAC ADDRESSS(AUTO)")]
		public string MACADDRESSSAUTO { get; set; }

		[JsonProperty("HDCP")]
		public string HDCP { get; set; }

		[JsonProperty("FIRMWARE VERSION")]
		public string FIRMWAREVERSION { get; set; }

		[JsonProperty("DECODERS LIGHTS-ALL")]
		public string CHIPSET { get; set; }

		[JsonProperty("ENCODERS LIGHTS-ALL")]
		public string ENCODERSLIGHTSALL { get; set; }
	}

	internal class OutputsDecoder
	{
		[JsonProperty("ONLINE")]
		public string ONLINE { get; set; }

		[JsonProperty("CUSTOM NAME")]
		public string CUSTOMNAME { get; set; }

		[JsonProperty("DESCRIPTION")]
		public string DESCRIPTION { get; set; }

		[JsonProperty("OUTPUT RESOLUTION")]
		public string OUTPUTRESOLUTION { get; set; }

		[JsonProperty("RETAIN HDR METADATA")]
		public string RETAINHDRMETADATA { get; set; }

		[JsonProperty("DISPLAY ROTATION")]
		public string DISPLAYROTATION { get; set; }

		[JsonProperty("VIDEO SCALE")]
		public string VIDEOSCALE { get; set; }

		[JsonProperty("IP ADDRESS(AUTO)")]
		public string IPADDRESSAUTO { get; set; }

		[JsonProperty("MAC ADDRESSS(AUTO)")]
		public string MACADDRESSSAUTO { get; set; }

		[JsonProperty("HDCP")]
		public string HDCP { get; set; }

		[JsonProperty("OSD - ALL")]
		public string OSDALL { get; set; }

		[JsonProperty("FIRMWARE VERSION")]
		public string FIRMWAREVERSION { get; set; }

		[JsonProperty("CHIPSET")]
		public string CHIPSET { get; set; }

		[JsonProperty("DECODERS LIGHTS-ALL")]
		public string DECODERSLIGHTSALL { get; set; }
	}

	internal class MxnetConfig
	{
		public List<InputEncoder> input_encoders { get; set; }
		public List<OutputsDecoder> outputs_decoders { get; set; }
	}
}
