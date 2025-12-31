using System.Text.Json;
using SYSTEM.HeadlessClient.Auth;
using SYSTEM.HeadlessClient.Mining;
using SYSTEM.HeadlessClient.Sensing;
using SYSTEM.HeadlessClient.World;

// Plan command uses WorldManager constants for timing calculation

namespace SYSTEM.HeadlessClient.Api;

/// <summary>
/// Handles API commands by delegating to the appropriate system.
/// </summary>
public class CommandHandler
{
    private readonly AuthManager _auth;
    private readonly WorldManager? _worldManager;
    private readonly SourceDetector? _sourceDetector;
    private readonly MiningController? _miningController;
    private readonly DateTime _startTime;

    public CommandHandler(
        AuthManager auth,
        WorldManager? worldManager,
        SourceDetector? sourceDetector,
        MiningController? miningController,
        DateTime startTime)
    {
        _auth = auth;
        _worldManager = worldManager;
        _sourceDetector = sourceDetector;
        _miningController = miningController;
        _startTime = startTime;
    }

    /// <summary>
    /// GET /status - Returns current QAI status
    /// </summary>
    public object GetStatus()
    {
        var uptime = DateTime.UtcNow - _startTime;

        return new
        {
            status = "running",
            uptime = new
            {
                seconds = uptime.TotalSeconds,
                formatted = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}"
            },
            player = _auth.LocalPlayer != null ? new
            {
                id = _auth.LocalPlayer.PlayerId,
                name = _auth.LocalPlayer.Name,
                world = new
                {
                    x = _auth.LocalPlayer.CurrentWorld.X,
                    y = _auth.LocalPlayer.CurrentWorld.Y,
                    z = _auth.LocalPlayer.CurrentWorld.Z
                }
            } : null,
            position = _worldManager != null ? new
            {
                x = _worldManager.Position.X,
                y = _worldManager.Position.Y,
                z = _worldManager.Position.Z
            } : null,
            mining = _miningController != null ? new
            {
                isMining = _miningController.IsMining,
                currentSourceId = _miningController.CurrentSourceId,
                currentSessionId = _miningController.CurrentSessionId,
                status = _miningController.GetMiningStatus()
            } : null,
            sensing = _sourceDetector != null ? new
            {
                sourcesInRange = _sourceDetector.SourcesInRange.Count
            } : null
        };
    }

    /// <summary>
    /// GET /sources - Returns list of sources in range
    /// </summary>
    public object GetSources()
    {
        if (_sourceDetector == null || _worldManager == null)
        {
            return new { error = "Systems not initialized" };
        }

        var sources = _sourceDetector.SourcesInRange.Select(s => new
        {
            sourceId = s.SourceId,
            position = new { x = s.Position.X, y = s.Position.Y, z = s.Position.Z },
            distance = WorldManager.Distance(_worldManager.Position, s.Position),
            totalPackets = s.TotalWavePackets,
            composition = s.WavePacketComposition.Where(c => c.Count > 0).Select(c => new
            {
                frequency = c.Frequency,
                color = GetFrequencyColor(c.Frequency),
                count = c.Count
            })
        }).OrderBy(s => s.distance).ToList();

        return new
        {
            count = sources.Count,
            sources
        };
    }

    /// <summary>
    /// POST /mine/start - Start mining
    /// Body: { "sourceId": 123 } or { "mode": "closest" } or { "mode": "richest" }
    /// </summary>
    public object StartMining(string body)
    {
        if (_miningController == null)
        {
            return new { success = false, error = "Mining controller not initialized" };
        }

        try
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                // Default: mine closest source
                var success = _miningController.StartMiningClosestSource();
                return new { success, message = success ? "Started mining closest source" : "No sources in range" };
            }

            var options = JsonSerializer.Deserialize<MineStartOptions>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (options?.SourceId.HasValue == true)
            {
                _miningController.StartMiningWithDefaultCrystal(options.SourceId.Value);
                return new { success = true, message = $"Started mining source {options.SourceId.Value}" };
            }

            if (options?.Mode?.ToLower() == "richest")
            {
                var success = _miningController.StartMiningRichestSource();
                return new { success, message = success ? "Started mining richest source" : "No sources in range" };
            }

            // Default: closest
            var result = _miningController.StartMiningClosestSource();
            return new { success = result, message = result ? "Started mining closest source" : "No sources in range" };
        }
        catch (JsonException ex)
        {
            return new { success = false, error = $"Invalid JSON: {ex.Message}" };
        }
    }

    /// <summary>
    /// POST /mine/stop - Stop current mining
    /// </summary>
    public object StopMining()
    {
        if (_miningController == null)
        {
            return new { success = false, error = "Mining controller not initialized" };
        }

        if (!_miningController.IsMining)
        {
            return new { success = false, error = "Not currently mining" };
        }

        _miningController.StopMining();
        return new { success = true, message = "Mining stopped" };
    }

    /// <summary>
    /// POST /move - Move QAI
    /// Body: { "forward": 5.0, "right": 0.0 } or { "yaw": 45.0 }
    /// </summary>
    public object Move(string body)
    {
        if (_worldManager == null)
        {
            return new { success = false, error = "World manager not initialized" };
        }

        try
        {
            var options = JsonSerializer.Deserialize<MoveOptions>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (options == null)
            {
                return new { success = false, error = "Invalid move options" };
            }

            if (options.Yaw.HasValue)
            {
                _worldManager.Rotate(options.Yaw.Value);
            }

            if (options.Forward.HasValue || options.Right.HasValue)
            {
                _worldManager.Move(options.Forward ?? 0, options.Right ?? 0);
            }

            // Force position update
            _worldManager.SendPositionUpdate();

            return new
            {
                success = true,
                position = new
                {
                    x = _worldManager.Position.X,
                    y = _worldManager.Position.Y,
                    z = _worldManager.Position.Z
                }
            };
        }
        catch (JsonException ex)
        {
            return new { success = false, error = $"Invalid JSON: {ex.Message}" };
        }
    }

    /// <summary>
    /// POST /scan - Force a source scan
    /// </summary>
    public object ForceScan()
    {
        if (_sourceDetector == null)
        {
            return new { success = false, error = "Source detector not initialized" };
        }

        _sourceDetector.ScanForSources();
        return new
        {
            success = true,
            sourcesFound = _sourceDetector.SourcesInRange.Count
        };
    }

    /// <summary>
    /// POST /walk - Start continuous walking (like holding WASD)
    /// Body: { "forward": 1, "duration": 5 } - walk forward for 5 seconds
    /// Body: { "forward": 1, "distance": 50 } - walk forward for 50 units
    /// Body: { "forward": 1 } - walk forward indefinitely
    /// Body: { "stop": true } - stop walking
    /// </summary>
    public object Walk(string body)
    {
        if (_worldManager == null)
        {
            return new { success = false, error = "World manager not initialized" };
        }

        try
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                // Return current walking status
                return new
                {
                    success = true,
                    walking = _worldManager.GetWalkingStatus()
                };
            }

            var options = JsonSerializer.Deserialize<WalkOptions>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (options == null)
            {
                return new { success = false, error = "Invalid walk options" };
            }

            // Stop command
            if (options.Stop == true)
            {
                _worldManager.StopWalking();
                _worldManager.SendPositionUpdate();
                return new
                {
                    success = true,
                    message = "Stopped walking",
                    position = new { x = _worldManager.Position.X, y = _worldManager.Position.Y, z = _worldManager.Position.Z }
                };
            }

            // Reset to start command
            if (options.Reset == true)
            {
                _worldManager.ResetToStart();
                _worldManager.SendPositionUpdate();
                return new
                {
                    success = true,
                    message = "Reset to starting position",
                    position = new { x = _worldManager.Position.X, y = _worldManager.Position.Y, z = _worldManager.Position.Z }
                };
            }

            // Rotate command (radians)
            if (options.Rotate.HasValue)
            {
                float radians = options.Rotate.Value;
                float degrees = radians * 180f / MathF.PI;
                _worldManager.Rotate(degrees);
                _worldManager.SendPositionUpdate();
                return new
                {
                    success = true,
                    message = $"Rotated {radians:F2} radians ({degrees:F1}°)",
                    position = new { x = _worldManager.Position.X, y = _worldManager.Position.Y, z = _worldManager.Position.Z }
                };
            }

            float forward = options.Forward ?? 0;
            float right = options.Right ?? 0;

            if (Math.Abs(forward) < 0.001f && Math.Abs(right) < 0.001f)
            {
                return new { success = false, error = "Must specify forward and/or right direction" };
            }

            // Duration mode
            if (options.Duration.HasValue)
            {
                _worldManager.StartWalkingForDuration(forward, right, options.Duration.Value);
                return new
                {
                    success = true,
                    message = $"Walking for {options.Duration.Value:F1} seconds",
                    walking = _worldManager.GetWalkingStatus()
                };
            }

            // Distance mode
            if (options.Distance.HasValue)
            {
                _worldManager.StartWalkingForDistance(forward, right, options.Distance.Value);
                return new
                {
                    success = true,
                    message = $"Walking for {options.Distance.Value:F1} units",
                    walking = _worldManager.GetWalkingStatus()
                };
            }

            // Indefinite mode
            _worldManager.StartWalking(forward, right);
            return new
            {
                success = true,
                message = "Walking indefinitely (use stop to end)",
                walking = _worldManager.GetWalkingStatus()
            };
        }
        catch (JsonException ex)
        {
            return new { success = false, error = $"Invalid JSON: {ex.Message}" };
        }
    }

    /// <summary>
    /// GET /walk - Get current walking status
    /// </summary>
    public object GetWalkingStatus()
    {
        if (_worldManager == null)
        {
            return new { success = false, error = "World manager not initialized" };
        }

        return new
        {
            success = true,
            walking = _worldManager.GetWalkingStatus(),
            position = new { x = _worldManager.Position.X, y = _worldManager.Position.Y, z = _worldManager.Position.Z }
        };
    }

    /// <summary>
    /// POST /plan - Execute a plan of commands
    /// Body: { "commands": [
    ///   { "type": "walk", "forward": 1, "distance": 50 },
    ///   { "type": "rotate", "radians": 1.5708 },
    ///   { "type": "walk", "forward": 1, "distance": 30 },
    ///   { "type": "wait", "seconds": 2 }
    /// ]}
    /// Timing is calculated automatically based on WALK_SPEED (10 units/sec).
    /// </summary>
    public object ExecutePlan(string body)
    {
        if (_worldManager == null)
        {
            return new { success = false, error = "World manager not initialized" };
        }

        try
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                // Return current plan status
                return new
                {
                    success = true,
                    plan = _worldManager.GetPlanStatus()
                };
            }

            var options = JsonSerializer.Deserialize<PlanOptions>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (options == null || options.Commands == null || options.Commands.Count == 0)
            {
                return new { success = false, error = "No commands provided" };
            }

            // Cancel command
            if (options.Cancel == true)
            {
                _worldManager.CancelPlan();
                return new { success = true, message = "Plan cancelled" };
            }

            // Convert JSON commands to PlanCommand objects
            var commands = new List<PlanCommand>();
            float totalEstimatedTime = 0;

            foreach (var cmd in options.Commands)
            {
                var planCmd = new PlanCommand();

                switch (cmd.Type?.ToLower())
                {
                    case "walk":
                        planCmd.Type = PlanCommandType.Walk;
                        planCmd.Forward = cmd.Forward ?? 1;
                        planCmd.Right = cmd.Right ?? 0;
                        planCmd.Distance = cmd.Distance ?? 10;
                        totalEstimatedTime += planCmd.Distance / WorldManager.WALK_SPEED;
                        break;

                    case "rotate":
                        planCmd.Type = PlanCommandType.Rotate;
                        planCmd.Radians = cmd.Radians ?? 0;
                        totalEstimatedTime += 0.1f; // Small buffer for rotation
                        break;

                    case "wait":
                        planCmd.Type = PlanCommandType.Wait;
                        planCmd.Seconds = cmd.Seconds ?? 1;
                        totalEstimatedTime += planCmd.Seconds;
                        break;

                    default:
                        return new { success = false, error = $"Unknown command type: {cmd.Type}" };
                }

                commands.Add(planCmd);
            }

            // Execute the plan
            _worldManager.QueuePlan(commands);

            return new
            {
                success = true,
                message = $"Plan started with {commands.Count} commands",
                estimatedDuration = totalEstimatedTime,
                plan = _worldManager.GetPlanStatus()
            };
        }
        catch (JsonException ex)
        {
            return new { success = false, error = $"Invalid JSON: {ex.Message}" };
        }
    }

    /// <summary>
    /// GET /plan - Get current plan status
    /// </summary>
    public object GetPlanStatus()
    {
        if (_worldManager == null)
        {
            return new { success = false, error = "World manager not initialized" };
        }

        return new
        {
            success = true,
            plan = _worldManager.GetPlanStatus(),
            position = new { x = _worldManager.Position.X, y = _worldManager.Position.Y, z = _worldManager.Position.Z }
        };
    }

    private static string GetFrequencyColor(float frequency)
    {
        if (frequency < 0.5f) return "red";
        if (frequency < 1.3f) return "yellow";
        if (frequency < 2.5f) return "green";
        if (frequency < 3.5f) return "cyan";
        if (frequency < 4.5f) return "blue";
        return "magenta";
    }

    private class MineStartOptions
    {
        public ulong? SourceId { get; set; }
        public string? Mode { get; set; }
    }

    private class MoveOptions
    {
        public float? Forward { get; set; }
        public float? Right { get; set; }
        public float? Yaw { get; set; }
    }

    private class WalkOptions
    {
        public float? Forward { get; set; }
        public float? Right { get; set; }
        public float? Duration { get; set; }  // seconds
        public float? Distance { get; set; }  // units
        public float? Rotate { get; set; }    // radians (positive = right, negative = left)
        public bool? Stop { get; set; }
        public bool? Reset { get; set; }
    }

    private class PlanOptions
    {
        public List<PlanCommandJson>? Commands { get; set; }
        public bool? Cancel { get; set; }
    }

    private class PlanCommandJson
    {
        public string? Type { get; set; }     // "walk", "rotate", "wait"
        public float? Forward { get; set; }   // for walk: -1 to 1
        public float? Right { get; set; }     // for walk: -1 to 1
        public float? Distance { get; set; }  // for walk: units
        public float? Radians { get; set; }   // for rotate: radians (+right, -left)
        public float? Seconds { get; set; }   // for wait: seconds
    }
}
