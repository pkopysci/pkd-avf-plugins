using pkd_hardware_service.VideoWallDevices;

namespace VideoWallEmulation;

public class EmulatedVideoWallCanvas : VideoWallCanvas
{
    public string ActiveLayoutId { get; set; } = string.Empty;

    public bool TryGetVideoWallCell(string cellId, out VideoWallCell? cell)
    {
        var layout = Layouts.FirstOrDefault(x => x.Id.Equals(ActiveLayoutId));
        if (layout == null)
        {
            cell = null;
            return false;
        }
        
        cell = layout.Cells.FirstOrDefault(x => x.Id.Equals(cellId));
        return cell != null;
    }
}