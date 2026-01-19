using ExileCore;
using ExileCore.Shared.Enums;
using SharpDX;

namespace Visual.Render;

/// <summary>
/// Renders team information above entities using direct memory reading.
/// Based on teams.md research:
/// GameObject + 0x90 -> Positioned*
/// Positioned + 0x1E0 -> Team (uint16)
/// Bits 0-14 = Team ID, Bit 15 = Is Minion flag
/// </summary>
public class TeamRenderer : IRenderComponent
{
    // GameObject + 0x90 -> Positioned*
    private const int PositionedPtrOffset = 0x90;
    // Positioned + 0x1E0 -> Team (uint16)
    private const int TeamOffset = 0x1E0;

    public bool Enabled { get; set; } = false;
    public bool DebugLog { get; set; } = true;
    public float VerticalOffset { get; set; } = -50f;

    public void Render()
    {
        if (!Enabled || !Global.InGame)
            return;

        var camera = Global.Camera;
        var memory = Global.Controller.Memory;
        var entityCount = 0;
        var renderedCount = 0;

        foreach (var entity in Global.Entities)
        {
            if (
                entity.Type == EntityType.MiscellaneousObjects || 
                entity.Type == EntityType.IngameIcon || 
                entity.Type == EntityType.AreaTransition
                )
              continue;

            entityCount++;

            if (!entity.IsValid)
                continue;

            // Read Positioned pointer from Entity (GameObject + 0x90)
            var positionedPtr = memory.Read<long>(entity.Address + PositionedPtrOffset);
            if (positionedPtr == 0)
            {
                continue;
            }

            // Read team from Positioned + 0x1E0
            var teamRaw = memory.Read<ushort>(positionedPtr + TeamOffset);
            var teamId = teamRaw & 0x7FFF;
            var isMinion = (teamRaw & 0x8000) != 0;

            var screenPos = camera.WorldToScreen(entity.PosNum);

            if (screenPos.X < 0 || screenPos.Y < 0 ||
                screenPos.X > camera.Width || screenPos.Y > camera.Height)
                continue;

            var color = GetTeamColor(teamId, isMinion);
            //var label = GetTeamLabel(teamId, isMinion);
            var label = teamId;

            var textPos = new System.Numerics.Vector2(screenPos.X, screenPos.Y + VerticalOffset);
            Global.Graphics.DrawText(entity.Type.ToString()+ " " +label,  textPos, color, FontAlign.Center);
            renderedCount++;
        }
    }

    private static Color GetTeamColor(int teamId, bool isMinion)
    {
        if (isMinion)
            return Color.Cyan;

        return teamId switch
        {
            0 => Color.Red,              // Monster base
            1 => Color.Green,            // Player
            69 => Color.LightGreen,      // Minion controller
            70 => Color.Yellow,          // Neutral
            >= 2 and <= 68 => Color.Orange,      // Monsters
            >= 71 and <= 119 => Color.OrangeRed, // Extended monsters
            >= 120 and <= 125 => Color.Purple,   // Environment (anti-player)
            >= 126 and <= 127 => Color.Blue,     // Environment (anti-monster)
            >= 128 => Color.LightBlue,   // Player minions
            _ => Color.White
        };
    }

    private static string GetTeamLabel(int teamId, bool isMinion)
    {
        var minionFlag = isMinion ? " [M]" : "";

        return teamId switch
        {
            0 => $"T0:Monster{minionFlag}",
            1 => $"T1:Player{minionFlag}",
            69 => $"T69:MinionCtrl{minionFlag}",
            70 => $"T70:Neutral{minionFlag}",
            >= 2 and <= 68 => $"T{teamId}:Mob{minionFlag}",
            >= 71 and <= 119 => $"T{teamId}:MobExt{minionFlag}",
            >= 120 and <= 125 => $"T{teamId}:EnvAntiPlr{minionFlag}",
            >= 126 and <= 127 => $"T{teamId}:EnvAntiMob{minionFlag}",
            >= 128 => $"T{teamId}:PlrMinion{minionFlag}",
            _ => $"T{teamId}{minionFlag}"
        };
    }
}
