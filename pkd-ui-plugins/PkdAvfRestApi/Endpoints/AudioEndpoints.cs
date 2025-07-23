using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using pkd_application_service;
using pkd_application_service.AudioControl;
using pkd_application_service.Base;
using pkd_common_utils.Logging;
using PkdAvfRestApi.Contracts;
using PkdAvfRestApi.Contracts.Audio;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
// ReSharper disable RedundantCast

namespace PkdAvfRestApi.Endpoints;

internal static class AudioEndpoints
{
    private static IAudioControlApp? _appService;

    public static RouteGroupBuilder MapAudioEndpoints(this WebApplication app, IApplicationService appService)
    {
        _appService = appService as IAudioControlApp;

        var group = app.MapGroup("audio");

        group.MapGet("/supported", () => Results.Ok(new SupportedDto(_appService != null)));

        group.MapGet("/dsps", () =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                List<AudioDspDto> dsps = [];
                foreach (var dsp in _appService.GetAllAudioDspDevices())
                {
                    dsps.Add(CreateDspDto(dsp));
                }

                return Results.Ok(dsps);
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST API - GET audio dsps.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/dsps/{id}", (string id) =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                var found = _appService.GetAllAudioDspDevices().FirstOrDefault(x => x.Id == id);
                return found == null ? Results.NotFound() : Results.Ok(CreateDspDto(found));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - GET audio dsp {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/inputs", () =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                List<AudioInputDto> inputs = [];
                foreach (var input in _appService.GetAudioInputChannels())
                {
                    inputs.Add(CreateAudioInputDto(input));
                }

                return Results.Ok(inputs);
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST API - GET audio inputs.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/inputs/{id}", (string id) =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                var found = _appService.GetAudioInputChannels().FirstOrDefault(x => x.Id == id);
                return found == null ? Results.NotFound() : Results.Ok(CreateAudioInputDto(found));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - GET audio input {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/outputs", () =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                List<AudioOutputDto> outputs = [];
                foreach (var output in _appService.GetAudioOutputChannels())
                {
                    outputs.Add(CreateAudioOutputDto(output));
                }

                return Results.Ok(outputs);
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST API - GET audio outputs.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapGet("/outputs/{id}", (string id) =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                var found = _appService.GetAudioOutputChannels().FirstOrDefault(x => x.Id == id);
                return found == null ? Results.NotFound() : Results.Ok(CreateAudioOutputDto(found));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - GET audio output {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/inputs/{id}", (string id, SetAudioInputDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                var found = _appService.GetAudioInputChannels().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound();

                if (_appService.QueryAudioInputMute(id) != body.Mute) _appService.SetAudioInputMute(id, body.Mute);
                if (_appService.QueryAudioInputLevel(id) != body.Level) _appService.SetAudioInputLevel(id, body.Level);

                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT audio input {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/inputs/{id}/level", (string id, SetAudioLevelDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                var found = _appService.GetAudioInputChannels().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound();
                if (_appService.QueryAudioInputLevel(id) != body.Level) _appService.SetAudioInputLevel(id, body.Level);
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT audio input {id}.");
                return Results.Problem("Internal Server Error");
            }
        });
        
        group.MapPut("/inputs/{id}/mute", (string id, SetAudioMuteDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                var found = _appService.GetAudioInputChannels().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound();
                if (_appService.QueryAudioInputMute(id) != body.Mute) _appService.SetAudioInputMute(id, body.Mute);
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT audio input {id}.");
                return Results.Problem("Internal Server Error");
            }
        });
        
        group.MapPut("/outputs/{id}/level", (string id, SetAudioLevelDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                var found = _appService.GetAudioOutputChannels().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound();
                if (_appService.QueryAudioOutputLevel(id) != body.Level) _appService.SetAudioOutputLevel(id, body.Level);
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT audio input {id}.");
                return Results.Problem("Internal Server Error");
            }
        });
        
        group.MapPut("/outputs/{id}/mute", (string id, SetAudioMuteDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                var found = _appService.GetAudioOutputChannels().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound();
                if (_appService.QueryAudioOutputMute(id) != body.Mute) _appService.SetAudioOutputMute(id, body.Mute);
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT audio input {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("outputs/{id}", (string id, SetAudioOutputDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                var found = _appService.GetAudioOutputChannels().FirstOrDefault(x => x.Id == id);
                if (found == null) return Results.NotFound();

                if (_appService.QueryAudioOutputMute(id) != body.Mute) _appService.SetAudioOutputMute(id, body.Mute);
                if (_appService.QueryAudioOutputLevel(id) != body.Level) _appService.SetAudioOutputLevel(id, body.Level);
                
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"AVF REST API - PUT output input {id}.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/routing/makeRoute", (SetAudioRouteDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                var input = _appService.GetAudioInputChannels().FirstOrDefault(x => x.Id == body.Input);
                if (input == null) return Results.NotFound();
                
                var output = _appService.GetAudioOutputChannels().FirstOrDefault(x => x.Id == body.Output);
                if (output == null) return Results.NotFound();

                if (_appService.QueryAudioOutputRoute(output.Id) != input.Id)
                {
                    _appService.SetAudioOutputRoute(input.Id, output.Id);
                }
                
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST API - PUT Audio route.");
                return Results.Problem("Internal Server Error");
            }
        });

        group.MapPut("/zoneEnable", (SetAudioZoneDto body) =>
        {
            if (_appService == null) return Results.BadRequest("Audio control not supported.");
            try
            {
                var input = _appService.GetAudioInputChannels().FirstOrDefault(x => x.Id == body.Input);
                if (input == null) return Results.NotFound("Input");

                if (input.ZoneEnableControls.FindIndex(x => x.Id == body.Zone) < 0)
                {
                    return Results.NotFound("Zone");
                }

                if (_appService.QueryAudioZoneState(body.Input, body.Zone) != body.Enable)
                {
                    _appService.ToggleAudioZoneState(body.Input, body.Zone);
                }
                
                return Results.NoContent();
            }
            catch (Exception e)
            {
                Logger.Error(e, "AVF REST API - PUT Audio route.");
                return Results.Problem("Internal Server Error");
            }
        });

        return group;
    }

    private static AudioDspDto CreateDspDto(InfoContainer dsp)
    {
        return new AudioDspDto(
            Id: dsp.Id,
            Label: dsp.Label,
            Manufacturer: dsp.Manufacturer,
            Model: dsp.Model,
            Icon: dsp.Icon,
            IsOnline: dsp.IsOnline,
            Tags: dsp.Tags
        );
    }

    private static AudioInputDto CreateAudioInputDto(AudioChannelInfoContainer input)
    {
        List<ZoneControlDto> zones = [];
        foreach (var zone in input.ZoneEnableControls)
        {
            zones.Add(new ZoneControlDto(
                Id: zone.Id,
                Label: zone.Label,
                Enabled: _appService != null && _appService.QueryAudioZoneState(input.Id, zone.Id)
            ));
        }

        return new AudioInputDto(
            Id: input.Id,
            Label: input.Label,
            Icon: input.Icon,
            Mute: _appService != null && _appService.QueryAudioInputMute(input.Id),
            Level: _appService?.QueryAudioInputLevel(input.Id) ?? 0,
            Tags: input.Tags,
            ZoneControls: zones
        );
    }

    private static AudioOutputDto CreateAudioOutputDto(AudioChannelInfoContainer output)
    {
        return new AudioOutputDto(
            Id: output.Id,
            Label: output.Label,
            Icon: output.Icon,
            RoutedInput: _appService?.QueryAudioOutputRoute(output.Id) ?? string.Empty,
            Mute: _appService != null && _appService.QueryAudioOutputMute(output.Id),
            Level: _appService?.QueryAudioOutputLevel(output.Id) ?? 0,
            Tags: output.Tags
        );
    }
}