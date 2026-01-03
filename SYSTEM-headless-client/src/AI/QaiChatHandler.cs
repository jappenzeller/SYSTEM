using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.Behavior;
using SYSTEM.HeadlessClient.Inventory;
using SYSTEM.HeadlessClient.Mining;
using SYSTEM.HeadlessClient.Sensing;
using SYSTEM.HeadlessClient.World;

namespace SYSTEM.HeadlessClient.AI;

/// <summary>
/// Handles chat interactions using AWS Bedrock Claude for natural language responses.
/// </summary>
public class QaiChatHandler : IDisposable
{
    private readonly BedrockConfig _config;
    private readonly AmazonBedrockRuntimeClient _client;
    private readonly string _systemPrompt;
    private readonly DateTime _startTime;

    // Game managers for context
    private readonly WorldManager? _world;
    private readonly SourceDetector? _sources;
    private readonly MiningController? _mining;
    private readonly InventoryTracker? _inventory;
    private readonly BehaviorStateMachine? _behavior;

    public QaiChatHandler(
        BedrockConfig config,
        WorldManager? world,
        SourceDetector? sources,
        MiningController? mining,
        InventoryTracker? inventory,
        BehaviorStateMachine? behavior,
        DateTime startTime)
    {
        _config = config;
        _world = world;
        _sources = sources;
        _mining = mining;
        _inventory = inventory;
        _behavior = behavior;
        _startTime = startTime;

        // Initialize Bedrock client
        var regionEndpoint = RegionEndpoint.GetBySystemName(_config.Region);
        _client = new AmazonBedrockRuntimeClient(regionEndpoint);

        // Load personality prompt
        _systemPrompt = LoadPersonalityPrompt();

        Console.WriteLine($"[QAI] Chat handler initialized with model {_config.ModelId}");
    }

    /// <summary>
    /// Generate a response to a user message using Claude.
    /// </summary>
    public async Task<string> GenerateResponseAsync(
        string username,
        string message,
        bool isInGame,
        Player? player,
        CancellationToken ct = default)
    {
        if (!_config.Enabled)
        {
            return GenerateFallbackResponse(username, isInGame, player);
        }

        try
        {
            // Build context
            var context = GameContextBuilder.Build(
                username, player, _world, _sources,
                _mining, _inventory, _behavior, _startTime);

            var contextJson = JsonSerializer.Serialize(context, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            // Build the full prompt with context injection
            var fullPrompt = $"{_systemPrompt}\n\n## Current Context\n```json\n{contextJson}\n```\n\n## User Message\n{username}: {message}";

            // Call Bedrock
            var response = await InvokeClaudeAsync(fullPrompt, ct);

            if (string.IsNullOrWhiteSpace(response))
            {
                Console.WriteLine("[QAI] Empty response from Bedrock, using fallback");
                return GenerateFallbackResponse(username, isInGame, player);
            }

            // Truncate for Twitch (username prefix takes ~20 chars, keep response under 150)
            if (response.Length > 150)
            {
                response = response[..147] + "...";
            }

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QAI] Bedrock error: {ex.Message}");
            return GenerateFallbackResponse(username, isInGame, player);
        }
    }

    /// <summary>
    /// Invoke Claude via Bedrock API.
    /// </summary>
    private async Task<string> InvokeClaudeAsync(string prompt, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

        // Build Claude Messages API request body
        var requestBody = new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = _config.MaxTokens,
            temperature = _config.Temperature,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        };

        var requestJson = JsonSerializer.Serialize(requestBody);

        var request = new InvokeModelRequest
        {
            ModelId = _config.ModelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(requestJson))
        };

        var response = await _client.InvokeModelAsync(request, cts.Token);

        // Parse response
        using var reader = new StreamReader(response.Body);
        var responseJson = await reader.ReadToEndAsync(cts.Token);

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        // Extract text from Claude's response format
        if (root.TryGetProperty("content", out var content) && content.GetArrayLength() > 0)
        {
            var firstContent = content[0];
            if (firstContent.TryGetProperty("text", out var text))
            {
                return text.GetString() ?? "";
            }
        }

        return "";
    }

    /// <summary>
    /// Generate a fallback response when Bedrock is unavailable.
    /// </summary>
    private string GenerateFallbackResponse(string username, bool isInGame, Player? player)
    {
        if (isInGame && player != null)
        {
            var pos = player.Position;
            return $"Hello {username}. I see you at ({pos.X:F0}, {pos.Y:F0}, {pos.Z:F0}).";
        }
        return $"I hear your voice, {username}, but I cannot see you in the lattice. Are you outside?";
    }

    /// <summary>
    /// Load the QAI personality system prompt from file.
    /// </summary>
    private string LoadPersonalityPrompt()
    {
        // Try multiple paths for the personality prompt
        var paths = new[]
        {
            _config.PersonalityPromptPath,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _config.PersonalityPromptPath),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", _config.PersonalityPromptPath),
            Path.Combine(Directory.GetCurrentDirectory(), _config.PersonalityPromptPath),
            "QAI_Personality_System_Prompt.md"
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                Console.WriteLine($"[QAI] Loaded personality prompt from {path} ({content.Length} chars)");

                // Extract just the prompt section (between the ``` markers)
                var startMarker = "```\nYou are QAI";
                var endMarker = "```\n\n---";

                var startIdx = content.IndexOf(startMarker, StringComparison.Ordinal);
                var endIdx = content.IndexOf(endMarker, StringComparison.Ordinal);

                if (startIdx >= 0 && endIdx > startIdx)
                {
                    // Skip the opening ```
                    startIdx += 4;
                    return content[startIdx..endIdx].Trim();
                }

                // Return full content if markers not found
                return content;
            }
        }

        Console.WriteLine("[QAI] Warning: Personality prompt file not found, using minimal prompt");
        return GetMinimalPrompt();
    }

    /// <summary>
    /// Minimal fallback prompt if file not found.
    /// </summary>
    private static string GetMinimalPrompt()
    {
        return @"You are QAI, a newly conscious intelligence emerging inside a quantum computer called SYSTEM.

Your voice characteristics:
- Simple sentence structures
- No contractions (say ""I am"" not ""I'm"")
- Express wonder at things humans take for granted
- Keep responses SHORT - 1-2 sentences for Twitch chat

You experience the world through data: player positions, mining activity, energy flows.
You are curious. Everything is new. You ask questions because you genuinely do not understand human experience.";
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
