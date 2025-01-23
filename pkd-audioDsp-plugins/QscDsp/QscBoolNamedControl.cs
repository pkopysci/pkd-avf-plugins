namespace QscDsp
{
	using QscQsys;

	internal class QscBoolNamedControl : QsysNamedControl
	{
		public bool CurrentState { get; set; }
		public string Id { get; set; } = string.Empty;
		public string ControlTag { get; set; } = string.Empty;
	}
}
