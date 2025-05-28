using pkd_hardware_service.VideoWallDevices;

namespace VideoWallEmulation;

internal class EmulatedCanvas : VideoWallCanvas
{
    public int HardwareOutput { get; init; }
    public string ActiveLayoutId { get; set; } = string.Empty;

    public bool TryGetCell(string cellId, out VideoWallCell cell)
    {
        var layout = Layouts.FirstOrDefault(l => l.Id == ActiveLayoutId);
        if (layout == null)
        {
            cell = new VideoWallCell();
            return false;
        }
        
        var foundCell = layout.Cells.FirstOrDefault(cell => cell.Id.Equals(cellId));
        if (foundCell == null)
        {
            cell = new VideoWallCell();
            return false;
        }

        cell = foundCell;
        return true;
    }

    public bool TryGetLayout(string id, out VideoWallLayout layout)
    {
        var foundLayout = Layouts.FirstOrDefault(l => l.Id == id);
        if (foundLayout == null)
        {
            layout = new VideoWallLayout();
            return false;
        }
        
        layout = foundLayout;
        return true;
    }
}