using System;
using System.Numerics;
using ExileCore;
using ExileCore.Shared.Enums;
using SharpDX;

namespace Visual.Render;

/// <summary>
/// Renders projectile information using direct memory reading.
///
/// Positioned Component Offsets (from IDA analysis):
///   +0x1E3 (483) - CollisionSize1 (sbyte)
///   +0x1E4 (484) - CollisionSize2 (sbyte)
///   +0x1E8 (488) - Trajectory data start
///   +0x1F0 (496) - Trajectory active flag (byte)
///   +0x1F1 (497) - Trajectory XY swap flag (byte)
///   +0x1F2 (498) - Trajectory Z swap flag (byte)
///   +0x214 (532) - Trajectory dest coord 1 (int)
///   +0x218 (536) - Trajectory dest coord 2 (int)
///   +0x21C (540) - Trajectory dest coord 3/Z (int)
///   +0x240 (576) - Movement duration (float)
///   +0x248 (584) - Speed (float)
///   +0x294 (660) - GridPosX (int)
///   +0x298 (664) - GridPosY (int)
///   +0x2A0 (672) - Movement angle (float, radians)
/// </summary>
public class ProjectileRenderer : IRenderComponent
{
    private const int PositionedPtrOffset = 0x90;
    private const int CollisionSize1Offset = 0x1E3;
    private const int CollisionSize2Offset = 0x1E4;
    private const int SpeedOffset = 0x248;
    private const int GridPosXOffset = 0x294;
    private const int GridPosYOffset = 0x298;

    // Trajectory data offsets (from Positioned_MoveData_Initialize)
    private const int TrajectoryActiveFlagOffset = 0x1F0; // +8 from trajectory start at 0x1E8
    private const int TrajectoryXYSwapFlagOffset = 0x1F1; // +9 from trajectory start
    private const int TrajectoryZSwapFlagOffset = 0x1F2;  // +10 from trajectory start
    private const int TrajectoryDestCoord1Offset = 0x214; // +0x2C from trajectory start
    private const int TrajectoryDestCoord2Offset = 0x218; // +0x30 from trajectory start
    private const int TrajectoryDestCoord3Offset = 0x21C; // +0x34 from trajectory start (Z related)
    private const int MovementDurationOffset = 0x240;
    private const int MovementAngleOffset = 0x2A0;

    // Grid to world conversion constant (250/23 ≈ 10.869565)
    private const float GridToWorld = 10.869565f;

    // Invalid coordinate marker
    private const int InvalidCoord = 0x7FFFFFFF;

    public bool Enabled { get; set; } = true;
    public float VerticalOffset { get; set; } = -30f;
    public float LineWidth { get; set; } = 2f;
    public float ArrowSize { get; set; } = 10f;
    public float FallbackProjectionDistance { get; set; } = 500f; // World units for angle-based projection

    public void Render()
    {
        if (!Enabled || !Global.InGame)
            return;

        var camera = Global.Camera;
        var memory = Global.Controller.Memory;

        foreach (var entity in Global.Entities)
        {
            if (!entity.IsValid)
                continue;

            var path = entity.Path;
            if (string.IsNullOrEmpty(path) || !path.Contains("Projectile", StringComparison.OrdinalIgnoreCase))
                continue;

            var positionedPtr = memory.Read<long>(entity.Address + PositionedPtrOffset);
            if (positionedPtr == 0)
                continue;

            var size1 = memory.Read<sbyte>(positionedPtr + CollisionSize1Offset);
            var size2 = memory.Read<sbyte>(positionedPtr + CollisionSize2Offset);
            var size = Math.Max(size1, size2);
            var speed = memory.Read<float>(positionedPtr + SpeedOffset);

            var screenPos = camera.WorldToScreen(entity.PosNum);
            if (screenPos.X < 0 || screenPos.Y < 0 ||
                screenPos.X > camera.Width || screenPos.Y > camera.Height)
                continue;

            // Read trajectory destination data
            var movementDuration = memory.Read<float>(positionedPtr + MovementDurationOffset);
            var movementAngle = memory.Read<float>(positionedPtr + MovementAngleOffset);
            var xySwapFlag = memory.Read<byte>(positionedPtr + TrajectoryXYSwapFlagOffset);
            var zSwapFlag = memory.Read<byte>(positionedPtr + TrajectoryZSwapFlagOffset);
            var destCoord1 = memory.Read<int>(positionedPtr + TrajectoryDestCoord1Offset);
            var destCoord2 = memory.Read<int>(positionedPtr + TrajectoryDestCoord2Offset);
            var destCoord3 = memory.Read<int>(positionedPtr + TrajectoryDestCoord3Offset);

            // Try to get destination from trajectory data
            System.Numerics.Vector3? destWorldPos = null;

            // Check if trajectory has valid destination data
            if (movementDuration > 0 && destCoord1 != InvalidCoord && destCoord2 != InvalidCoord)
            {
                int destGridX, destGridY;

                // Handle Z swap first (swaps with primary axis)
                if (zSwapFlag != 0)
                {
                    // Z swap active: coord3 contains what would be X or Y
                    if (xySwapFlag != 0)
                    {
                        destGridX = destCoord3;
                        destGridY = destCoord1;
                    }
                    else
                    {
                        destGridX = destCoord3;
                        destGridY = destCoord2;
                    }
                }
                else
                {
                    // No Z swap: standard XY handling
                    if (xySwapFlag != 0)
                    {
                        destGridX = destCoord2;
                        destGridY = destCoord1;
                    }
                    else
                    {
                        destGridX = destCoord1;
                        destGridY = destCoord2;
                    }
                }

                // Validate coordinates are reasonable (not too large)
                if (Math.Abs(destGridX) < 100000 && Math.Abs(destGridY) < 100000)
                {
                    var destWorldX = (destGridX + 0.5f) * GridToWorld;
                    var destWorldY = (destGridY + 0.5f) * GridToWorld;
                    destWorldPos = new System.Numerics.Vector3(destWorldX, destWorldY, entity.PosNum.Z);
                }
            }

            // Fallback: use movement angle to project destination
            if (!destWorldPos.HasValue && movementAngle != 0)
            {
                // Movement angle is typically offset by 90 degrees (PI/2) in game coords
                var adjustedAngle = movementAngle - MathF.PI / 2;
                var dirX = MathF.Cos(adjustedAngle);
                var dirY = MathF.Sin(adjustedAngle);
                destWorldPos = new System.Numerics.Vector3(
                    entity.PosNum.X + dirX * FallbackProjectionDistance,
                    entity.PosNum.Y + dirY * FallbackProjectionDistance,
                    entity.PosNum.Z);
            }

            // Draw if we have a valid destination
            if (destWorldPos.HasValue)
            {
                var destScreenPos = camera.WorldToScreen(destWorldPos.Value);

                // if (destScreenPos.X >= -100 && destScreenPos.Y >= -100 &&
                //     destScreenPos.X <= camera.Width + 100 && destScreenPos.Y <= camera.Height + 100)
                // {
                    var startVec = new System.Numerics.Vector2(screenPos.X, screenPos.Y);
                    var endVec = new System.Numerics.Vector2(destScreenPos.X, destScreenPos.Y);

                    // Draw the main line
                    Global.Graphics.DrawLine(startVec, endVec, LineWidth, Color.Cyan);

                    // Draw arrowhead at destination
                    DrawArrowhead(endVec, startVec, ArrowSize, Color.Cyan);

                    // Draw small circle at destination point
                    Global.Graphics.DrawCircleFilled(endVec, 4f, Color.Red, 8);
                // }
            }

            var label = $"Spd:{speed:F0} Sz:{size}";
            var textPos = new System.Numerics.Vector2(screenPos.X, screenPos.Y + VerticalOffset);
            Global.Graphics.DrawText(label, textPos, Color.Yellow, FontAlign.Center);
        }
    }

    private void DrawArrowhead(System.Numerics.Vector2 tip, System.Numerics.Vector2 from, float size, Color color)
    {
        // Calculate direction vector
        var dx = tip.X - from.X;
        var dy = tip.Y - from.Y;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        if (length < 0.001f)
            return;

        // Normalize direction
        dx /= length;
        dy /= length;

        // Perpendicular vector
        var px = -dy;
        var py = dx;

        // Arrowhead points
        var back = new System.Numerics.Vector2(tip.X - dx * size, tip.Y - dy * size);
        var left = new System.Numerics.Vector2(back.X + px * size * 0.5f, back.Y + py * size * 0.5f);
        var right = new System.Numerics.Vector2(back.X - px * size * 0.5f, back.Y - py * size * 0.5f);

        // Draw arrowhead as triangle
        Global.Graphics.DrawLine(tip, left, LineWidth, color);
        Global.Graphics.DrawLine(tip, right, LineWidth, color);
        Global.Graphics.DrawLine(left, right, LineWidth, color);
    }
}
