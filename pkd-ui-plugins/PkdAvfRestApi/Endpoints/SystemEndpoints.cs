using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using pkd_application_service;
using pkd_common_utils.Logging;
using PkdAvfRestApi.Contracts.System;

namespace PkdAvfRestApi.Endpoints;

internal static class SystemEndpoints
{
    private static IApplicationService? _appService;

    public static RouteGroupBuilder MapSystemEndpoints(this WebApplication app, IApplicationService service)
    {
        _appService = service;
        var group = app.MapGroup("system");

        group.MapGet("/info", () =>
        {
            try
            {
                var roomInfo = _appService.GetRoomInfo();
                var dto = new SystemInfoDto(
                    Id: roomInfo.Id,
                    Label: roomInfo.Label,
                    Manufacturer: string.Empty,
                    Model: string.Empty,
                    HelpContact: roomInfo.HelpContact,
                    SystemType: roomInfo.SystemType,
                    Tags: roomInfo.Tags);

                return Results.Ok(dto);
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST API - Get Room Info");
                return Results.Problem("Internal Server Error");
            }
        }).WithName("Info");

        group.MapGet("/userInterfaces", () =>
        {
            try
            {
                var allUis = _appService.GetAllUserInterfaces();
                List<UserInterfaceDto> dtos = [];
                foreach (var ui in allUis)
                {
                    List<MenuItemDto> menu = [];
                    foreach (var item in ui.MenuItems)
                    {
                        menu.Add(new MenuItemDto(
                            Id: item.Id,
                            Label: item.Label,
                            Control: item.Control,
                            Icon: item.Icon,
                            Tags: item.Tags));
                    }


                    dtos.Add(new UserInterfaceDto(
                        Id: ui.Id,
                        Label: ui.Label,
                        Manufacturer: ui.Manufacturer,
                        Model: ui.Model,
                        Tags: ui.Tags,
                        IsOnline: ui.IsOnline,
                        IpId: ui.IpId,
                        MenuItems: menu
                    ));
                }

                return Results.Ok(dtos);
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST API - Get User Interfaces");
                return Results.Problem("Internal Server Error");
            }
        }).WithName("UserInterfaces");

        group.MapGet("/userInterfaces/{id}", (string id) =>
        {
            try
            {
                var found = _appService.GetAllUserInterfaces().FirstOrDefault(u => u.Id == id);
                if (found == null)
                {
                    return Results.NotFound();
                }

                List<MenuItemDto> menu = [];
                foreach (var item in found.MenuItems)
                {
                    menu.Add(new MenuItemDto(
                        Id: item.Id,
                        Label: item.Label,
                        Control: item.Control,
                        Icon: item.Icon,
                        Tags: item.Tags
                    ));
                }

                return Results.Ok(new UserInterfaceDto(
                    Id: found.Id,
                    Label: found.Label,
                    Manufacturer: found.Manufacturer,
                    Model: found.Model,
                    Tags: found.Tags,
                    IsOnline: found.IsOnline,
                    IpId: found.IpId,
                    MenuItems: menu
                ));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - Get User Interface {id}");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/state", () =>
        {
            try
            {
                return Results.Ok(new UseStateDto(State: _appService.CurrentSystemState));
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST APIs - Get State");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/state", (UpdateStateDto stateDto) =>
        {
            try
            {
                if (stateDto.State)
                {
                    _appService.SetActive();
                }
                else
                {
                    _appService.SetStandby();
                }
                
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST API - Put State");
                return Results.Problem("Internal Server Error");
            }
        });

        return group;
    }
}