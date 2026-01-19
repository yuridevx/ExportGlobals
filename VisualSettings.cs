using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace Visual;

public class VisualSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
}
