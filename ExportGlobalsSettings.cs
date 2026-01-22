using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace ExportGlobals;

public class ExportGlobalsSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    public ToggleNode EnableMcpServer { get; set; } = new ToggleNode(true);

    public RangeNode<int> McpPort { get; set; } = new RangeNode<int>(5099, 1024, 65535);
}
