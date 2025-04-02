using Crestron.SimplSharpPro;

namespace PkdAvfRestApi.Tools;

public interface IControlSystemContext
{
    CrestronControlSystem ControlSystem { get; }
}