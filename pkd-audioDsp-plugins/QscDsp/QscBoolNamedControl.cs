namespace QscDsp
{
	using QscQsys;

	internal class QscBoolNamedControl : QsysNamedControl
	{
		public bool CurrentState { get; set; }
		public string Id { get; set; }
		public string ControlTag { get; set; }
	}
}
