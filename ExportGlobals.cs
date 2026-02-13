using System;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExportGlobals.Mcp;

namespace ExportGlobals;

public class ExportGlobals : BaseSettingsPlugin<ExportGlobalsSettings>
{
    private McpServer _mcpServer;
    private AutoReloadListener _autoReloadListener;

    public override bool Initialise()
    {
        Global.Init(this, GameController, Graphics, Settings);

        // Start MCP server
        StartMcpServer();

        // Restart server when port changes
        Settings.McpPort.OnValueChanged += (_, _) =>
        {
            StopMcpServer();
            StartMcpServer();
        };

        Settings.EnableMcpServer.OnValueChanged += (_, _) =>
        {
            if (Settings.EnableMcpServer)
                StartMcpServer();
            else
                StopMcpServer();
        };

        // Start auto-reload listener (always on)
        _autoReloadListener = new AutoReloadListener(GameController, LogMessage, LogError);
        _autoReloadListener.Start();

        return true;
    }

    private void StartMcpServer()
    {
        if (!Settings.EnableMcpServer) return;
        if (_mcpServer?.IsRunning == true) return;

        try
        {
            _mcpServer = new McpServer(Settings.McpPort);
            _mcpServer.OnLog += message => LogMessage(message);
            _mcpServer.Start();
            LogMessage($"MCP server started on {_mcpServer.Endpoint}");
        }
        catch (Exception ex)
        {
            LogError($"Failed to start MCP server: {ex.Message}");
        }
    }

    private void StopMcpServer()
    {
        if (_mcpServer == null) return;

        try
        {
            _mcpServer.Stop();
            _mcpServer.Dispose();
            _mcpServer = null;
            LogMessage("MCP server stopped");
        }
        catch (Exception ex)
        {
            LogError($"Error stopping MCP server: {ex.Message}");
        }
    }

    public override void AreaChange(AreaInstance area)
    {
    }

    public override Job Tick()
    {
        return null;
    }

    public override void Render()
    {
    }

    public override void EntityAdded(Entity entity)
    {
    }

    public override void OnUnload()
    {
        _autoReloadListener?.Dispose();
        StopMcpServer();
        base.OnUnload();
    }
}
