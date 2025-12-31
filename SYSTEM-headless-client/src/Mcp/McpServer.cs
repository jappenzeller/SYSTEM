using System.Text.Json;
using SYSTEM.HeadlessClient.Api;
using SYSTEM.HeadlessClient.Auth;
using SYSTEM.HeadlessClient.Behavior;
using SYSTEM.HeadlessClient.Inventory;
using SYSTEM.HeadlessClient.Mining;
using SYSTEM.HeadlessClient.Sensing;
using SYSTEM.HeadlessClient.World;

namespace SYSTEM.HeadlessClient.Mcp;

/// <summary>
/// MCP server handling tools and resources for AI orchestration integration.
/// </summary>
public class McpServer
{
    private readonly AuthManager _auth;
    private readonly WorldManager? _worldManager;
    private readonly SourceDetector? _sourceDetector;
    private readonly MiningController? _miningController;
    private readonly InventoryTracker? _inventoryTracker;
    private readonly BehaviorStateMachine? _behaviorStateMachine;
    private readonly DateTime _startTime;

    private bool _initialized;

    public McpServer(
        AuthManager auth,
        WorldManager? worldManager,
        SourceDetector? sourceDetector,
        MiningController? miningController,
        InventoryTracker? inventoryTracker,
        BehaviorStateMachine? behaviorStateMachine,
        DateTime startTime)
    {
        _auth = auth;
        _worldManager = worldManager;
        _sourceDetector = sourceDetector;
        _miningController = miningController;
        _inventoryTracker = inventoryTracker;
        _behaviorStateMachine = behaviorStateMachine;
        _startTime = startTime;
    }

    /// <summary>
    /// Handle an incoming MCP request.
    /// </summary>
    public McpResponse HandleRequest(McpRequest request)
    {
        try
        {
            return request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "initialized" => HandleInitialized(request),
                "tools/list" => HandleToolsList(request),
                "tools/call" => HandleToolsCall(request),
                "resources/list" => HandleResourcesList(request),
                "resources/read" => HandleResourcesRead(request),
                _ => McpResponse.Failure(McpError.MethodNotFound, $"Unknown method: {request.Method}", request.Id)
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MCP] Error handling request: {ex.Message}");
            return McpResponse.Failure(McpError.InternalError, ex.Message, request.Id);
        }
    }

    private McpResponse HandleInitialize(McpRequest request)
    {
        var result = new McpInitializeResult
        {
            ProtocolVersion = "2024-11-05",
            Capabilities = new McpServerCapabilities
            {
                Tools = new McpToolsCapability { ListChanged = false },
                Resources = new McpResourcesCapability { Subscribe = false, ListChanged = false }
            },
            ServerInfo = new McpServerInfo
            {
                Name = "qai-agent",
                Version = "1.0.0"
            }
        };

        return McpResponse.Success(result, request.Id);
    }

    private McpResponse HandleInitialized(McpRequest request)
    {
        _initialized = true;
        Console.WriteLine("[MCP] Client initialized");
        return McpResponse.Success(null, request.Id);
    }

    private McpResponse HandleToolsList(McpRequest request)
    {
        var tools = new List<McpTool>
        {
            new McpTool
            {
                Name = "get_status",
                Description = "Get current QAI agent status including position, mining state, and inventory",
                InputSchema = new McpInputSchema { Properties = new() }
            },
            new McpTool
            {
                Name = "mine_start",
                Description = "Start mining a wave packet source",
                InputSchema = new McpInputSchema
                {
                    Properties = new()
                    {
                        ["sourceId"] = new McpPropertySchema { Type = "integer", Description = "Optional source ID to mine" },
                        ["mode"] = new McpPropertySchema { Type = "string", Description = "Selection mode: 'closest' or 'richest'", Enum = new() { "closest", "richest" } }
                    }
                }
            },
            new McpTool
            {
                Name = "mine_stop",
                Description = "Stop current mining operation",
                InputSchema = new McpInputSchema { Properties = new() }
            },
            new McpTool
            {
                Name = "walk",
                Description = "Walk in a direction for a specified distance",
                InputSchema = new McpInputSchema
                {
                    Properties = new()
                    {
                        ["forward"] = new McpPropertySchema { Type = "number", Description = "Forward direction (-1 to 1)" },
                        ["right"] = new McpPropertySchema { Type = "number", Description = "Right direction (-1 to 1)" },
                        ["distance"] = new McpPropertySchema { Type = "number", Description = "Distance to walk in units" }
                    },
                    Required = new() { "forward", "distance" }
                }
            },
            new McpTool
            {
                Name = "walk_stop",
                Description = "Stop walking immediately",
                InputSchema = new McpInputSchema { Properties = new() }
            },
            new McpTool
            {
                Name = "scan",
                Description = "Scan for nearby wave packet sources",
                InputSchema = new McpInputSchema { Properties = new() }
            }
        };

        return McpResponse.Success(new { tools }, request.Id);
    }

    private McpResponse HandleToolsCall(McpRequest request)
    {
        if (request.Params == null)
        {
            return McpResponse.Failure(McpError.InvalidParams, "Missing params", request.Id);
        }

        var paramsJson = request.Params.Value.GetRawText();
        var callParams = JsonSerializer.Deserialize<McpToolCallParams>(paramsJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (callParams == null)
        {
            return McpResponse.Failure(McpError.InvalidParams, "Invalid tool call params", request.Id);
        }

        var result = callParams.Name switch
        {
            "get_status" => ExecuteGetStatus(),
            "mine_start" => ExecuteMineStart(callParams.Arguments),
            "mine_stop" => ExecuteMineStop(),
            "walk" => ExecuteWalk(callParams.Arguments),
            "walk_stop" => ExecuteWalkStop(),
            "scan" => ExecuteScan(),
            _ => new McpToolResult
            {
                IsError = true,
                Content = new() { new McpToolContent { Text = $"Unknown tool: {callParams.Name}" } }
            }
        };

        return McpResponse.Success(result, request.Id);
    }

    private McpResponse HandleResourcesList(McpRequest request)
    {
        var resources = new List<McpResource>
        {
            new McpResource
            {
                Uri = "qai://status",
                Name = "Agent Status",
                Description = "Current QAI agent state, position, and mining status"
            },
            new McpResource
            {
                Uri = "qai://sources",
                Name = "Wave Packet Sources",
                Description = "Nearby wave packet sources with distances and compositions"
            },
            new McpResource
            {
                Uri = "qai://inventory",
                Name = "Inventory",
                Description = "Current inventory contents and capacity"
            }
        };

        return McpResponse.Success(new { resources }, request.Id);
    }

    private McpResponse HandleResourcesRead(McpRequest request)
    {
        if (request.Params == null)
        {
            return McpResponse.Failure(McpError.InvalidParams, "Missing params", request.Id);
        }

        var paramsJson = request.Params.Value.GetRawText();
        var readParams = JsonSerializer.Deserialize<McpReadResourceParams>(paramsJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (readParams == null)
        {
            return McpResponse.Failure(McpError.InvalidParams, "Invalid read params", request.Id);
        }

        var contents = readParams.Uri switch
        {
            "qai://status" => ReadStatusResource(),
            "qai://sources" => ReadSourcesResource(),
            "qai://inventory" => ReadInventoryResource(),
            _ => new McpResourceContents
            {
                Uri = readParams.Uri,
                Text = JsonSerializer.Serialize(new { error = $"Unknown resource: {readParams.Uri}" })
            }
        };

        return McpResponse.Success(new { contents = new[] { contents } }, request.Id);
    }

    #region Tool Implementations

    private McpToolResult ExecuteGetStatus()
    {
        var uptime = DateTime.UtcNow - _startTime;
        var status = new
        {
            uptime = new { seconds = uptime.TotalSeconds, formatted = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}" },
            player = _auth.LocalPlayer != null ? new { id = _auth.LocalPlayer.PlayerId, name = _auth.LocalPlayer.Name } : null,
            position = _worldManager != null ? new { x = _worldManager.Position.X, y = _worldManager.Position.Y, z = _worldManager.Position.Z } : null,
            mining = _miningController != null ? new { active = _miningController.IsMining, sourceId = _miningController.CurrentSourceId } : null,
            inventory = _inventoryTracker != null ? new { count = _inventoryTracker.TotalCount, capacity = InventoryTracker.MAX_CAPACITY, full = _inventoryTracker.IsFull } : null,
            behavior = _behaviorStateMachine?.CurrentState.ToString() ?? "unknown"
        };

        return new McpToolResult
        {
            Content = new() { new McpToolContent { Text = JsonSerializer.Serialize(status) } }
        };
    }

    private McpToolResult ExecuteMineStart(JsonElement? arguments)
    {
        if (_miningController == null)
        {
            return new McpToolResult { IsError = true, Content = new() { new McpToolContent { Text = "Mining controller not available" } } };
        }

        bool success;
        string message;

        if (arguments?.TryGetProperty("sourceId", out var sourceIdProp) == true)
        {
            var sourceId = sourceIdProp.GetUInt64();
            _miningController.StartMiningWithDefaultCrystal(sourceId);
            success = true;
            message = $"Started mining source {sourceId}";
        }
        else
        {
            var mode = arguments?.TryGetProperty("mode", out var modeProp) == true ? modeProp.GetString() : "closest";
            success = mode == "richest" ? _miningController.StartMiningRichestSource() : _miningController.StartMiningClosestSource();
            message = success ? $"Started mining {mode} source" : "No sources in range";
        }

        return new McpToolResult
        {
            IsError = !success,
            Content = new() { new McpToolContent { Text = JsonSerializer.Serialize(new { success, message }) } }
        };
    }

    private McpToolResult ExecuteMineStop()
    {
        if (_miningController == null)
        {
            return new McpToolResult { IsError = true, Content = new() { new McpToolContent { Text = "Mining controller not available" } } };
        }

        if (!_miningController.IsMining)
        {
            return new McpToolResult { IsError = true, Content = new() { new McpToolContent { Text = "Not currently mining" } } };
        }

        _miningController.StopMining();
        return new McpToolResult { Content = new() { new McpToolContent { Text = JsonSerializer.Serialize(new { success = true, message = "Mining stopped" }) } } };
    }

    private McpToolResult ExecuteWalk(JsonElement? arguments)
    {
        if (_worldManager == null)
        {
            return new McpToolResult { IsError = true, Content = new() { new McpToolContent { Text = "World manager not available" } } };
        }

        float forward = 0, right = 0, distance = 10;
        if (arguments?.TryGetProperty("forward", out var forwardProp) == true)
            forward = forwardProp.GetSingle();
        if (arguments?.TryGetProperty("right", out var rightProp) == true)
            right = rightProp.GetSingle();
        if (arguments?.TryGetProperty("distance", out var distProp) == true)
            distance = distProp.GetSingle();

        _worldManager.StartWalkingForDistance(forward, right, distance);
        return new McpToolResult
        {
            Content = new() { new McpToolContent { Text = JsonSerializer.Serialize(new { success = true, message = $"Walking {distance} units" }) } }
        };
    }

    private McpToolResult ExecuteWalkStop()
    {
        if (_worldManager == null)
        {
            return new McpToolResult { IsError = true, Content = new() { new McpToolContent { Text = "World manager not available" } } };
        }

        _worldManager.StopWalking();
        return new McpToolResult { Content = new() { new McpToolContent { Text = JsonSerializer.Serialize(new { success = true, message = "Stopped walking" }) } } };
    }

    private McpToolResult ExecuteScan()
    {
        if (_sourceDetector == null)
        {
            return new McpToolResult { IsError = true, Content = new() { new McpToolContent { Text = "Source detector not available" } } };
        }

        _sourceDetector.ScanForSources();
        return new McpToolResult
        {
            Content = new() { new McpToolContent { Text = JsonSerializer.Serialize(new { success = true, sourcesFound = _sourceDetector.SourcesInRange.Count }) } }
        };
    }

    #endregion

    #region Resource Implementations

    private McpResourceContents ReadStatusResource()
    {
        var uptime = DateTime.UtcNow - _startTime;
        var status = new
        {
            uptime = uptime.TotalSeconds,
            player = _auth.LocalPlayer?.Name,
            position = _worldManager != null ? new { _worldManager.Position.X, _worldManager.Position.Y, _worldManager.Position.Z } : null,
            mining = _miningController?.IsMining ?? false,
            behavior = _behaviorStateMachine?.CurrentState.ToString() ?? "unknown"
        };

        return new McpResourceContents
        {
            Uri = "qai://status",
            Text = JsonSerializer.Serialize(status)
        };
    }

    private McpResourceContents ReadSourcesResource()
    {
        var sources = _sourceDetector?.SourcesInRange.Select(s => new
        {
            sourceId = s.SourceId,
            position = new { s.Position.X, s.Position.Y, s.Position.Z },
            distance = _worldManager != null ? WorldManager.Distance(_worldManager.Position, s.Position) : 0,
            totalPackets = s.TotalWavePackets
        }).OrderBy(s => s.distance).ToList() ?? new();

        return new McpResourceContents
        {
            Uri = "qai://sources",
            Text = JsonSerializer.Serialize(new { count = sources.Count, sources })
        };
    }

    private McpResourceContents ReadInventoryResource()
    {
        var inventory = new
        {
            totalCount = _inventoryTracker?.TotalCount ?? 0,
            capacity = InventoryTracker.MAX_CAPACITY,
            isFull = _inventoryTracker?.IsFull ?? false,
            composition = _inventoryTracker?.GetCompositionSummary() ?? new Dictionary<string, int>()
        };

        return new McpResourceContents
        {
            Uri = "qai://inventory",
            Text = JsonSerializer.Serialize(inventory)
        };
    }

    #endregion
}
