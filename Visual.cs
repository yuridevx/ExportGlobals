using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Interfaces;
using SharpDX;
using Visual.Render;

namespace Visual;

public class Visual : BaseSettingsPlugin<VisualSettings>
{
    private const int PositionedPtrOffset = 0x90;

    private readonly List<IRenderComponent> _renderComponents = new();


    public override bool Initialise()
    {
        Global.Init(this, GameController, Graphics, Settings);

        _renderComponents.Add(new TeamRenderer
        {
            VerticalOffset = -50f,
        });

        _renderComponents.Add(new ProjectileRenderer
        {
            VerticalOffset = -30f,
        });

        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
    }

    public override Job Tick()
    {
        if (!GameController.InGame)
            return null;

        var player = GameController.Player;
        if (player == null)
            return null;

        return null;
    }

    public override void Render()
    {
        foreach (var component in _renderComponents)
        {
            component.Render();
        }
    }

    public override void EntityAdded(Entity entity)
    {
    }
}
