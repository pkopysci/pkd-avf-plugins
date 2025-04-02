using Crestron.SimplSharpPro;

namespace PkdAvfRestApi.Tools;

public sealed class ControlSystemContext : IControlSystemContext
{
    public CrestronControlSystem ControlSystem { get; }

    public ControlSystemContext(CrestronControlSystem controlSystem)
    {
        ControlSystem = controlSystem;
    }
}