namespace CrComLibUi;

using pkd_application_service.UserInterface;
using pkd_ui_service.Interfaces;

internal interface ICrComLibComponent : IUiComponent
{
	UserInterfaceDataContainer UiData { get; set; }
}