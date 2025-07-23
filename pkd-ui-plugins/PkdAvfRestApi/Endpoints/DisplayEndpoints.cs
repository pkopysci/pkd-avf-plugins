using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using pkd_application_service;
using pkd_application_service.DisplayControl;
using pkd_common_utils.Logging;
using PkdAvfRestApi.Contracts;
using PkdAvfRestApi.Contracts.Video.Displays;

namespace PkdAvfRestApi.Endpoints;

internal static class DisplayEndpoints
{
    private const string GetDisplayEndpointName = "GetDisplay";
    private static IDisplayControlApp? _appService;

    public static RouteGroupBuilder MapDisplayEndpoints(this WebApplication app, IApplicationService service)
    {
        // IApplicationService may not always implement IDisplayControl App in the future.
        // ReSharper disable once RedundantCast
        _appService = service as IDisplayControlApp;

        var group = app.MapGroup("video/displays");

        group.MapGet("/supported", () => Results.Ok(new SupportedDto(_appService != null)));
        
        group.MapGet("/", () =>
            {
                if (_appService == null) return Results.Ok(new List<DisplayDto>());
                try
                {
                    var displays = _appService.GetAllDisplayInfo();
                    List<DisplayDto> dtos = [];
                    foreach (var display in displays)
                    {
                        dtos.Add(GetDisplayDto(display));
                    }

                    return Results.Ok(dtos);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "AVF REST API - Get ALl Displays");
                    return Results.Problem("Internal Server Error");
                }
            })
            .WithName("Displays");

        group.MapGet("/{id}", (string id) =>
            {
                if (_appService == null) return Results.BadRequest();
                try
                {
                    var found = _appService.GetAllDisplayInfo().FirstOrDefault(x => x.Id == id);
                    return found == null ? Results.NotFound() : Results.Ok(GetDisplayDto(found));
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"AVF REST API - Get Display {id}");
                    return Results.Problem("Internal Server Error");
                }
            })
            .WithName(GetDisplayEndpointName);

        group.MapPut("/{id}", (string id, UpdateDisplayDto dto) =>
        {
            if (_appService == null) return Results.BadRequest();
            try
            {
                var found = _appService.GetAllDisplayInfo().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound();

                var displayPower = _appService.DisplayPowerQuery(id);
                if (displayPower && _appService.DisplayFreezeQuery(id) != dto.IsFrozen)
                {
                    _appService.SetDisplayFreeze(id, dto.IsFrozen);
                }

                if (displayPower && _appService.DisplayBlankQuery(id) != dto.IsBlank)
                {
                    _appService.SetDisplayBlank(id, dto.IsBlank);
                }

                if (displayPower != dto.PowerState)
                {
                    _appService.SetDisplayPower(id, dto.PowerState);
                }

                // TODO: DisplayEndpoints - set display input selected once it is supported.
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT Display {id}");
                return Results.Problem("Internal Server Error");
            }

            return Results.NoContent();
        });

        group.MapPut("/screen/{id}", (string id, UpdateScreenDto dto) =>
        {
            if (_appService == null) return Results.BadRequest();
            try
            {
                var found = _appService.GetAllDisplayInfo().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound();
                if (dto.Position)
                {
                    _appService.LowerScreen(id);
                }
                else
                {
                    _appService.RaiseScreen(id);
                }

                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT Screen for display {id}");
                return Results.Problem("Internal Server Error");
            }
        });

        return group;
    }

    private static DisplayDto GetDisplayDto(DisplayInfoContainer display)
    {
        List<DisplayInputDto> inputDtos = [];
        foreach (var input in display.Inputs)
        {
            var isSelected = (_appService != null) && ((input.Tags.Contains("lectern") &&
                                                        _appService.DisplayInputLecternQuery(display.Id))
                                                       || (input.Tags.Contains("station") &&
                                                           _appService.DisplayInputStationQuery(display.Id)));

            inputDtos.Add(new DisplayInputDto(
                Id: input.Id,
                Label: input.Label,
                Tags: input.Tags,
                InputNumber: input.InputNumber,
                Selected: isSelected));
        }

        return new DisplayDto(
            Id: display.Id,
            Label: display.Label,
            Tags: display.Tags,
            Manufacturer: display.Manufacturer,
            Model: display.Model,
            Icon: display.Icon,
            IsOnline: display.IsOnline,
            IsFrozen: _appService != null && _appService.DisplayFreezeQuery(display.Id),
            IsBlank: _appService != null && _appService.DisplayBlankQuery(display.Id),
            PowerState: _appService != null && _appService.DisplayPowerQuery(display.Id),
            HasScreen: display.HasScreen,
            Inputs: inputDtos);
    }
}