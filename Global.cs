using System.Collections.Generic;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

namespace Visual;

/// <summary>
/// Static access to core plugin instances.
/// </summary>
public static class Global
{
    public static GameController Controller { get; private set; }
    public static Graphics Graphics { get; private set; }
    public static VisualSettings Settings { get; private set; }
    public static Visual Plugin { get; private set; }

    // Convenience accessors
    public static IngameState IngameState => Controller?.IngameState;
    public static Camera Camera => IngameState?.Camera;
    public static Entity Player => Controller?.Player;
    public static ICollection<Entity> Entities => Controller?.Entities;
    public static bool InGame => Controller?.InGame ?? false;

    internal static void Init(Visual plugin, GameController controller, Graphics graphics, VisualSettings settings)
    {
        Plugin = plugin;
        Controller = controller;
        Graphics = graphics;
        Settings = settings;
    }
}
