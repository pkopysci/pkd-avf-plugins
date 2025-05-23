﻿using pkd_domain_service.Data.RoutingData;
using pkd_hardware_service.VideoWallDevices;

namespace VideoWallEmulation;

public static class WallData
{
    public static List<EmulatedVideoWallCanvas> CreateCanvases()
    {
        List<VideoWallLayout> layouts =
        [
            new()
            {
                Id = "vw01",
                Label = "2x2 Grid",
                Width = 2,
                Height = 2,
                Cells =
                [
                    new VideoWallCell { Id = "vw01c01", XPosition = 1, YPosition = 1, DefaultSourceId = "vws01"},
                    new VideoWallCell { Id = "vw01c02", XPosition = 2, YPosition = 1, DefaultSourceId = "vws02" },
                    new VideoWallCell { Id = "vw01c03", XPosition = 1, YPosition = 2, DefaultSourceId = "vws03" },
                    new VideoWallCell { Id = "vw01c04", XPosition = 2, YPosition = 2, DefaultSourceId = "vws04" }
                ]
            },
            new()
            {
               Id = "vw02",
               Label = "4x2 Grid",
               Width = 4,
               Height = 2,
               Cells = [
                   new VideoWallCell { Id = "vw02c01", XPosition = 1, YPosition = 1, DefaultSourceId = "vws01" },
                   new VideoWallCell { Id = "vw02c02", XPosition = 2, YPosition = 1, DefaultSourceId = "vws02" },
                   new VideoWallCell { Id = "vw02c03", XPosition = 3, YPosition = 1, DefaultSourceId = "vws03" },
                   new VideoWallCell { Id = "vw02c04", XPosition = 4, YPosition = 1, DefaultSourceId = "vws04" },
                   new VideoWallCell { Id = "vw02c05", XPosition = 1, YPosition = 2, DefaultSourceId = "vws05" },
                   new VideoWallCell { Id = "vw02c06", XPosition = 2, YPosition = 2, DefaultSourceId = "vws06" },
                   new VideoWallCell { Id = "vw02c07", XPosition = 3, YPosition = 2, DefaultSourceId = "vws07" },
                   new VideoWallCell { Id = "vw02c08", XPosition = 4, YPosition = 2, DefaultSourceId = "vws08" },
               ]
            },
            new()
            {
                Id = "vw03",
                Label = "Dual Video",
                Width = 2,
                Height = 1,
                Cells = [
                    new VideoWallCell { Id = "vw03c01", XPosition = 1, YPosition = 1, DefaultSourceId = "vws01" },
                    new VideoWallCell { Id = "vw03c02", XPosition = 2, YPosition = 1, DefaultSourceId = "vws02" },
                ]
            },
            new()
            {
                Id = "vw04",
                Label = "Full Screen",
                Width = 1,
                Height = 1,
                Cells = [ new VideoWallCell { Id = "vw04c01", XPosition = 1, YPosition = 1, DefaultSourceId = "vws01"} ]
            }
        ];

        List<EmulatedVideoWallCanvas> canvases =
        [
            new ()
            {
                Id = "canv01",
                Label = "Canvas 1",
                Layouts = layouts,
                MaxHeight = 2,
                MaxWidth = 4,
                StartupLayoutId = "vw01",
                Tags = []
            }
        ];
        
        return canvases;
    }

    public static List<Source> CreateSources()
    {
        return
        [
            new Source()
            {
                Control = "ctv01", Id = "vws01", Label = "Cable TV 1", Icon = "cable-tv", Input = 1, Matrix = "sw01",
                Tags = []
            },
            new Source()
            {
                Control = "ctv02", Id = "vws02", Label = "Cable TV 2", Icon = "cable-tv", Input = 2, Matrix = "sw01",
                Tags = []
            },
            new Source()
            {
                Control = string.Empty, Id = "vws03", Label = "Station 1", Icon = "tv", Input = 3, Matrix = "sw01",
                Tags = []
            },
            new Source()
            {
                Control = string.Empty, Id = "vws04", Label = "Station 2", Icon = "tv", Input = 4, Matrix = "sw01",
                Tags = []
            },
            new Source()
            {
                Control = string.Empty, Id = "vws05", Label = "Station 3", Icon = "tv", Input = 5, Matrix = "sw01",
                Tags = []
            },
            new Source()
            {
                Control = string.Empty, Id = "vws06", Label = "Station 4", Icon = "tv", Input = 6, Matrix = "sw01",
                Tags = []
            },
            new Source()
            {
                Control = string.Empty, Id = "vws07", Label = "Station 5", Icon = "tv", Input = 7, Matrix = "sw01",
                Tags = []
            },
            new Source()
            {
                Control = string.Empty, Id = "vws08", Label = "Station 6", Icon = "tv", Input = 8, Matrix = "sw01",
                Tags = []
            }
        ];
    }
}