using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using pkd_application_service;
using pkd_application_service.AvRouting;
using pkd_application_service.Base;
using pkd_common_utils.Logging;
using PkdAvfRestApi.Contracts;
using PkdAvfRestApi.Contracts.Video.Routing;

namespace PkdAvfRestApi.Endpoints;

internal static class VideoRoutingEndpoints
{
    private static IAvRoutingApp? _appService;

    public static RouteGroupBuilder MapVideoRoutingEndpoints(this WebApplication app, IApplicationService appService)
    {
        // IApplicationService may not always implement IDisplayControl App in the future.
        // ReSharper disable once RedundantCast
        _appService = appService as IAvRoutingApp;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (_appService == null)
        {
            Logger.Warn(
                "AVF REST Api - Video Routing Endpoints - provided IApplicationService does not implement IAvRoutingApp.");
        }

        var group = app.MapGroup("video/routing");

        group.MapGet("/supported", () => Results.Ok(new SupportedDto(_appService != null)));
        
        group.MapGet("/inputs", () =>
        {
            if (_appService == null) return Results.BadRequest("Video routing not supported.");

            try
            {
                List<VideoInputDto> inputDtos = [];
                foreach (var input in _appService.GetAllAvSources())
                {
                    inputDtos.Add(CreateInputDto(input));
                }
                
                return Results.Ok(inputDtos);
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST API - GET video inputs.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/inputs/{id}", (string id) =>
        {
            if (_appService == null) return Results.BadRequest("Video routing not supported.");

            try
            {
                var found = _appService.GetAllAvSources().FirstOrDefault(a => a.Id == id);
                return found == null ? Results.NotFound() : Results.Ok(CreateInputDto(found));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - GET video input {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/outputs", () =>
        {
            if (_appService == null) return Results.BadRequest("Video routing not supported.");

            try
            {
                List<VideoOutputDto> outputDtos = [];
                foreach (var output in _appService.GetAllAvDestinations())
                {
                    outputDtos.Add(CreateOutputDto(output));   
                }
                
                return Results.Ok(outputDtos);
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST API - GET video outputs.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/outputs/{id}", (string id) =>
        {
            if (_appService == null) return Results.BadRequest("Video routing not supported.");
            try
            {
                var found = _appService.GetAllAvDestinations().FirstOrDefault(a => a.Id == id);
                return found == null ? Results.NotFound() : Results.Ok(CreateOutputDto(found));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - GET video output {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/avrs", () =>
        {
            if (_appService == null) return Results.BadRequest("Video routing not supported.");
            try
            {
                List<VideoAvrDto> avrDtos = [];
                foreach (var avr in _appService.GetAllAvRouters())
                {
                    avrDtos.Add(CreateAvrDto(avr));
                }
                
                return Results.Ok(avrDtos);
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST API - GET video AVRs.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/avrs/{id}", (string id) =>
        {
            if (_appService == null) return Results.BadRequest("Video routing not supported.");
            try
            {
                var found = _appService.GetAllAvRouters().FirstOrDefault(a => a.Id == id);
                return found == null ? Results.NotFound() : Results.Ok(CreateAvrDto(found));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - GET video AVR {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/makeRoute", (SetVideoRouteDto dto) =>
        {
            if (_appService == null) return Results.BadRequest("Video routing not supported.");
            try
            {
                var noInput = _appService.GetAllAvSources().FirstOrDefault(x => x.Id == dto.Input) == null;
                var noOutput = _appService.GetAllAvDestinations().FirstOrDefault(x => x.Id == dto.Output) == null;
                if (noInput || noOutput)
                {
                    return Results.NotFound("Input or Output id does not exist in system configuration.");
                }
                
                _appService.MakeRoute(dto.Input, dto.Output);
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT video route.");
                return Results.Problem("Internal Server Error");
            }
        });

        return group;
    }

    private static VideoInputDto CreateInputDto(AvSourceInfoContainer input)
    {
        return new VideoInputDto(
            Id: input.Id,
            Label: input.Label,
            Icon: input.Icon,
            HasSync: input.HasSync,
            SupportsSync: input.SupportSync,
            Tags: input.Tags);
    }

    private static VideoOutputDto CreateOutputDto(InfoContainer output)
    {
        var currentSource = _appService == null ? string.Empty : _appService.QueryCurrentRoute(output.Id).Id;
        return new VideoOutputDto(
            Id: output.Id,
            Label: output.Label,
            CurrentSource: currentSource,
            Tags: output.Tags,
            Icon: output.Icon
        );
    }

    private static VideoAvrDto CreateAvrDto(InfoContainer avr)
    {
        return new VideoAvrDto(
            Id: avr.Id,
            Label: avr.Label,
            Manufacturer: avr.Manufacturer,
            Model: avr.Model,
            Tags: avr.Tags,
            IsOnline: avr.IsOnline
        );
    }
}