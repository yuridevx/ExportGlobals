using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace ExportGlobals;

public class ExportGlobalsSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
}
