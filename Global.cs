using System;
using System.Collections.Generic;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

namespace ExportGlobals;

/// <summary>
/// Static access to core plugin instances and root object registry for MCP.
/// </summary>
public static class Global
{
    public static GameController Controller { get; private set; }
    public static Graphics Graphics { get; private set; }
    public static ExportGlobalsSettings Settings { get; private set; }
    public static ExportGlobals Plugin { get; private set; }

    // Convenience accessors
    public static IngameState IngameState => Controller?.IngameState;
    public static IngameUIElements IngameUi => IngameState?.IngameUi;
    public static Camera Camera => IngameState?.Camera;
    public static Entity Player => Controller?.Player;
    public static ICollection<Entity> Entities => Controller?.Entities;
    public static object Files => Controller?.Files;
    public static TheGame Memory => Controller?.Game;
    public static bool InGame => Controller?.InGame ?? false;

    internal static void Init(ExportGlobals plugin, GameController controller, Graphics graphics, ExportGlobalsSettings settings)
    {
        Plugin = plugin;
        Controller = controller;
        Graphics = graphics;
        Settings = settings;
    }

    /// <summary>
    /// Gets all available root objects for MCP exploration.
    /// </summary>
    public static Dictionary<string, Func<object>> GetRootObjects()
    {
        return new Dictionary<string, Func<object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["GameController"] = () => Controller,
            ["Player"] = () => Player,
            ["Entities"] = () => Entities,
            ["IngameState"] = () => IngameState,
            ["IngameUi"] = () => IngameUi,
            ["Camera"] = () => Camera,
            ["Files"] = () => Files,
            ["Memory"] = () => Memory,
            ["Graphics"] = () => Graphics,
            ["Settings"] = () => Settings
        };
    }

    /// <summary>
    /// Resolves a root object by name.
    /// </summary>
    public static object ResolveRoot(string name)
    {
        var roots = GetRootObjects();
        if (roots.TryGetValue(name, out var getter))
        {
            return getter();
        }
        return null;
    }
}
