namespace AvProEdgeAvSwitch
{
	using Newtonsoft.Json;
	using System.Collections.Generic;

	// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
	internal class InputEncoder
	{
		[JsonProperty("ONLINE")]
		public string Online { get; set; } = string.Empty;

		[JsonProperty("CUSTOM NAME")]
		public string CustomName { get; set; } = string.Empty;

		[JsonProperty("DESCRIPTION")]
		public string Description { get; set; } = string.Empty;

		[JsonProperty("EDID MGMT")]
		public string EdidManagement { get; set; } = string.Empty;

		[JsonProperty("CHANNEL")]
		public string Channel { get; set; } = string.Empty;

		[JsonProperty("IP ADDRESS(AUTO)")]
		public string IpAddressAuto { get; set; } = string.Empty;

		[JsonProperty("MAC ADDRESSS(AUTO)")]
		public string MacAddressAuto { get; set; } = string.Empty;

		[JsonProperty("Hdcp")]
		public string Hdcp { get; set; } = string.Empty;

		[JsonProperty("FIRMWARE VERSION")]
		public string FirmwareVersion { get; set; } = string.Empty;

		[JsonProperty("DECODERS LIGHTS-ALL")]
		public string DecodersLightsAll { get; set; } = string.Empty;

		[JsonProperty("ENCODERS LIGHTS-ALL")]
		public string EncodersLightsAll { get; set; } = string.Empty;
	}

	internal class OutputsDecoder
	{
		[JsonProperty("ONLINE")]
		public string Online { get; set; } = string.Empty;

		[JsonProperty("CUSTOM NAME")]
		public string CustomName { get; set; } = string.Empty;

		[JsonProperty("DESCRIPTION")]
		public string Description { get; set; } = string.Empty;

		[JsonProperty("OUTPUT RESOLUTION")]
		public string OutputResolution { get; set; } = string.Empty;

		[JsonProperty("RETAIN HDR METADATA")]
		public string RetainHdrMetadata { get; set; } = string.Empty;

		[JsonProperty("DISPLAY ROTATION")]
		public string DisplayRotation { get; set; } = string.Empty;

		[JsonProperty("VIDEO SCALE")]
		public string VideoScale { get; set; } = string.Empty;

		[JsonProperty("IP ADDRESS(AUTO)")]
		public string IpAddressAuto { get; set; } = string.Empty;

		[JsonProperty("MAC ADDRESSS(AUTO)")]
		public string MacAddressAuto { get; set; } = string.Empty;

		[JsonProperty("HDCP")]
		public string Hdcp { get; set; } = string.Empty;

		[JsonProperty("OSD - ALL")]
		public string OsdAll { get; set; } = string.Empty;

		[JsonProperty("FIRMWARE VERSION")]
		public string FirmwareVersion { get; set; } = string.Empty;

		[JsonProperty("CHIPSET")]
		public string Chipset { get; set; } = string.Empty;

		[JsonProperty("DECODERS LIGHTS-ALL")]
		public string DecoderLightsAll { get; set; } = string.Empty;
	}

	internal class MxnetConfig
	{
		public List<InputEncoder> InputEncoders { get; set; } = [];
		public List<OutputsDecoder> OutputDecoders { get; set; } = [];
	}
}
