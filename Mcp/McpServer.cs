using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExportGlobals.CodeExecution;
using ExportGlobals.CodeExecution.Results;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExportGlobals.Mcp;

/// <summary>
/// HTTP server implementing MCP protocol with JSON-RPC 2.0.
/// </summary>
public class McpServer : IDisposable
{
    private const string ProtocolVersion = "2024-11-05";
    private const string ServerName = "exile-api-mcp";
    private const string ServerVersion = "1.0.0";
    private const int DefaultTimeoutSeconds = 30;

    private HttpListener _listener;
    private CancellationTokenSource _cts;
    private Task _serverTask;
    private readonly CodeExecutionController _controller = new();
    private readonly int _port;
    private string _sessionId;

    public bool IsRunning => _listener?.IsListening ?? false;
    public string Endpoint => $"http://localhost:{_port}/mcp";

    public event Action<string> OnLog;

    public McpServer(int port = 5099)
    {
        _port = port;
    }

    public void Start()
    {
        if (IsRunning) return;

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Start();

            _cts = new CancellationTokenSource();
            _serverTask = Task.Run(() => ListenAsync(_cts.Token));

            Log($"MCP server started on {Endpoint}");
        }
        catch (Exception ex)
        {
            Log($"Failed to start MCP server: {ex.Message}");
            throw;
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;

        try
        {
            _cts?.Cancel();
            _listener?.Stop();
            _serverTask?.Wait(TimeSpan.FromSeconds(2));
            Log("MCP server stopped");
        }
        catch (Exception ex)
        {
            Log($"Error stopping MCP server: {ex.Message}");
        }
        finally
        {
            _listener?.Close();
            _listener = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequestAsync(context), ct);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"Listener error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, MCP-Protocol-Version, Mcp-Session-Id");

        try
        {
            if (context.Request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (path != "/mcp" && path != "/")
            {
                response.StatusCode = 404;
                response.Close();
                return;
            }

            if (context.Request.HttpMethod != "POST")
            {
                response.StatusCode = 405;
                response.Close();
                return;
            }

            string body;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            JObject requestObj;
            try
            {
                requestObj = JObject.Parse(body);
            }
            catch (JsonException)
            {
                await SendJsonRpcErrorAsync(response, null, -32700, "Parse error");
                return;
            }

            var id = requestObj["id"];
            var method = requestObj["method"]?.ToString();

            if (string.IsNullOrEmpty(method))
            {
                await SendJsonRpcErrorAsync(response, id, -32600, "Invalid request");
                return;
            }

            var result = await HandleMethodAsync(method, requestObj["params"], id);

            if (result == null)
            {
                response.StatusCode = 202;
                response.Close();
                return;
            }

            if (_sessionId != null)
                response.Headers.Add("Mcp-Session-Id", _sessionId);

            await SendResponseAsync(response, result);
        }
        catch (Exception ex)
        {
            Log($"Request error: {ex.Message}");
            await SendJsonRpcErrorAsync(response, null, -32603, ex.Message);
        }
    }

    private async Task<object> HandleMethodAsync(string method, JToken paramsToken, JToken id)
    {
        return method switch
        {
            "initialize" => HandleInitialize(id),
            "initialized" => null,
            "tools/list" => HandleToolsList(id),
            "tools/call" => await HandleToolsCallAsync(paramsToken, id),
            "ping" => CreateResponse(id, new { }),
            _ => CreateErrorResponse(id, -32601, $"Method not found: {method}")
        };
    }

    private object HandleInitialize(JToken id)
    {
        _sessionId = Guid.NewGuid().ToString("N");

        return CreateResponse(id, new Dictionary<string, object>
        {
            ["protocolVersion"] = ProtocolVersion,
            ["capabilities"] = new Dictionary<string, object>
            {
                ["tools"] = new Dictionary<string, object> { ["listChanged"] = false }
            },
            ["serverInfo"] = new Dictionary<string, object>
            {
                ["name"] = ServerName,
                ["version"] = ServerVersion
            }
        });
    }

    private object HandleToolsList(JToken id)
    {
        return CreateResponse(id, new Dictionary<string, object>
        {
            ["tools"] = GetToolDefinitions()
        });
    }

    private async Task<object> HandleToolsCallAsync(JToken paramsToken, JToken id)
    {
        var toolName = paramsToken?["name"]?.ToString();
        var arguments = paramsToken?["arguments"] as JObject;

        var (text, isError) = toolName switch
        {
            "execute" => FormatExecuteResult(await ExecuteWithTimeoutAsync(arguments)),
            _ => ($"Unknown tool: {toolName}", true)
        };

        return CreateResponse(id, new Dictionary<string, object>
        {
            ["content"] = new List<object>
            {
                new Dictionary<string, object> { ["type"] = "text", ["text"] = text }
            },
            ["isError"] = isError
        });
    }

    private async Task<ExecuteToolResult> ExecuteWithTimeoutAsync(JObject arguments)
    {
        var code = arguments?["code"]?.ToString();
        var timeoutSeconds = arguments?["timeout"]?.Value<int>() ?? DefaultTimeoutSeconds;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        return await _controller.ExecuteAsync(code, timeout);
    }

    private static (string text, bool isError) FormatExecuteResult(ExecuteToolResult result)
    {
        return result switch
        {
            ExecuteToolResult.CompletedResult r => ($"Result: {FormatObject(r.Result)}", false),
            ExecuteToolResult.FailureResult r => ($"Execution failed.\nError: {r.Error}", true),
            ExecuteToolResult.CompilationFailedResult r => ($"Compilation failed:\n{r.Errors}", true),
            _ => ("Unknown result", true)
        };
    }

    private static string FormatObject(object obj)
    {
        if (obj == null) return "null";
        try
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }
        catch
        {
            return obj.ToString();
        }
    }

    private static List<object> GetToolDefinitions()
    {
        return new List<object>
        {
            new Dictionary<string, object>
            {
                ["name"] = "execute",
                ["description"] = """
                    Execute C# code with full access to ExileCore game API.

                    ## Language & Runtime
                    - **C# 12** via Roslyn compilation
                    - **Class-based**: Code must define a public class with an Execute method
                    - **LINQ**: Full support for `.Where()`, `.Select()`, `.ToList()`, etc.
                    - **Async/await**: Standard C# async/await supported

                    ## Code Structure
                    Your code must define a public class with one of these Execute method signatures:
                    - `public async Task<object> Execute(CancellationToken ct)` (instance method)
                    - `public static async Task<object> Execute(CancellationToken ct)` (static method)

                    ## Available via ExportGlobals.Global
                    - `Global.Controller` - GameController (main entry point)
                    - `Global.Player` - Current player Entity
                    - `Global.Entities` - ICollection<Entity> of all loaded entities
                    - `Global.IngameState` - IngameState (Camera, Data, ServerData, IngameUi)
                    - `Global.IngameUi` - IngameUIElements (StashElement, InventoryPanel, Map, SkillBar, Atlas, TradeWindow, etc.)
                    - `Global.Camera` - Camera (WorldToScreen, Position, Size, ZoomLevel)
                    - `Global.Files` - FilesContainer (BaseItemTypes, Mods, Stats, PassiveSkills, WorldAreas, SkillGems, etc.)
                    - `Global.Memory` - TheGame (game states, AreaChangeCount, IsLoading)
                    - `Global.InGame` - bool check if in game

                    ## Entity Properties
                    Entity has: Id, Path, Metadata, RenderName, GridPos, Pos, DistancePlayer, IsValid, IsAlive, IsDead, IsHostile, IsTargetable, Rarity, Type, Buffs, Stats
                    Use `entity.GetComponent<T>()` to access components.

                    ## Key Components (ExileCore.PoEMemory.Components)
                    - **Life**: CurHP, MaxHP, CurES, MaxES, CurMana, MaxMana, HPPercentage, ESPercentage
                    - **Actor**: ActorSkills, Animation, CurrentAction, isMoving, isAttacking, DeployedObjects
                    - **Mods**: ItemMods, ExplicitMods, ImplicitMods, ItemLevel, ItemRarity, Identified, UniqueName
                    - **Render**: Pos, Bounds, Name, Height, TerrainHeight
                    - **Positioned**: GridPos, WorldPos, Rotation, Scale
                    - **Player**: Level, AllocatedLootId
                    - **Stats**: StatDictionary (all entity stats)
                    - **Buffs**: BuffsList
                    - **Chest**: IsOpened, IsLocked, IsStrongbox
                    - **Flask**: CurrentCharges, MaxCharges
                    - **Sockets**: SocketedGems, NumberOfSockets, Links
                    - **Stack**: Size, MaxStackSize
                    - **WorldItem**: ItemEntity (for ground items)
                    - **Monster**: Affixes, MonsterType
                    - **Map**: Tier, Area
                    - **SkillGem**: Level, QualityType
                    - **Quality**: ItemQuality

                    ## ServerData (via IngameState.ServerData)
                    PlayerInventories, PlayerStashTabs, PassiveSkillIds, League, Latency, CharacterLevel, Gold

                    ## IngameUIElements Key Panels
                    StashElement, InventoryPanel, Atlas, AtlasTreePanel, TreePanel (passive tree), Map, SkillBar, TradeWindow, SellWindow, NpcDialog, ChatBox, ItemsOnGroundLabels

                    ## No Pre-imported Namespaces
                    Scripts must import all namespaces explicitly.

                    ## Examples

                    ### Example 1: Simple query
                    ```csharp
                    using System.Threading;
                    using System.Threading.Tasks;
                    using ExportGlobals;

                    public class Script
                    {
                        public static async Task<object> Execute(CancellationToken ct)
                        {
                            return new {
                                InGame = Global.InGame,
                                PlayerPos = Global.Player?.GridPos
                            };
                        }
                    }
                    ```

                    ### Example 2: Query entities
                    ```csharp
                    using System.Linq;
                    using System.Threading;
                    using System.Threading.Tasks;
                    using ExportGlobals;

                    public class Script
                    {
                        public static async Task<object> Execute(CancellationToken ct)
                        {
                            var entities = Global.Entities?
                                .Where(e => e.IsValid && e.IsHostile)
                                .Take(10)
                                .Select(e => new { e.Id, e.RenderName, e.GridPos, e.Rarity })
                                .ToList();

                            return new { Count = entities?.Count ?? 0, Entities = entities };
                        }
                    }
                    ```

                    ### Example 3: Access player components
                    ```csharp
                    using System.Threading;
                    using System.Threading.Tasks;
                    using ExportGlobals;
                    using ExileCore.PoEMemory.Components;

                    public class Script
                    {
                        public static async Task<object> Execute(CancellationToken ct)
                        {
                            var player = Global.Player;
                            var life = player?.GetComponent<Life>();
                            var actor = player?.GetComponent<Actor>();

                            return new {
                                HP = life?.CurHP,
                                MaxHP = life?.MaxHP,
                                ES = life?.CurES,
                                MaxES = life?.MaxES,
                                IsMoving = actor?.isMoving,
                                Skills = actor?.ActorSkills?.Count
                            };
                        }
                    }
                    ```

                    ### Example 4: Read inventory items
                    ```csharp
                    using System.Linq;
                    using System.Threading;
                    using System.Threading.Tasks;
                    using ExportGlobals;
                    using ExileCore.PoEMemory.Components;

                    public class Script
                    {
                        public static async Task<object> Execute(CancellationToken ct)
                        {
                            var inventory = Global.IngameUi?.InventoryPanel;
                            var items = inventory?[ExileCore.Shared.Enums.InventoryIndex.PlayerInventory]?.VisibleInventoryItems?
                                .Where(i => i?.Item != null)
                                .Take(10)
                                .Select(i => new {
                                    Name = i.Item.GetComponent<Base>()?.Name,
                                    i.Item.Path
                                })
                                .ToList();

                            return items;
                        }
                    }
                    ```
                    """,
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["code"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "C# code defining a public class with an Execute method. Method signature: public async Task<object> Execute(CancellationToken ct)"
                        },
                        ["timeout"] = new Dictionary<string, object>
                        {
                            ["type"] = "integer",
                            ["description"] = "Timeout in seconds. Default is 30.",
                            ["default"] = 30
                        }
                    },
                    ["required"] = new[] { "code" }
                }
            }
        };
    }

    private static object CreateResponse(JToken id, object result)
    {
        return new Dictionary<string, object>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result
        };
    }

    private static object CreateErrorResponse(JToken id, int code, string message)
    {
        return new Dictionary<string, object>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new Dictionary<string, object>
            {
                ["code"] = code,
                ["message"] = message
            }
        };
    }

    private async Task SendResponseAsync(HttpListenerResponse response, object result)
    {
        var json = JsonConvert.SerializeObject(result, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        var buffer = Encoding.UTF8.GetBytes(json);
        response.StatusCode = 200;
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.Close();
    }

    private async Task SendJsonRpcErrorAsync(HttpListenerResponse response, JToken id, int code, string message)
    {
        var errorResponse = CreateErrorResponse(id, code, message);
        response.StatusCode = 200;
        await SendResponseAsync(response, errorResponse);
    }

    private void Log(string message)
    {
        OnLog?.Invoke($"[MCP] {message}");
    }

    public void Dispose()
    {
        Stop();
    }
}
