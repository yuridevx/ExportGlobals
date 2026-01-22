using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

namespace ExportGlobals;

public class ExportGlobals : BaseSettingsPlugin<ExportGlobalsSettings>
{
    public override bool Initialise()
    {
        Global.Init(this, GameController, Graphics, Settings);
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
    }

    public override void EntityAdded(Entity entity)
    {
    }
}
