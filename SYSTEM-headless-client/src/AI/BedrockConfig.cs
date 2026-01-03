namespace SYSTEM.HeadlessClient.AI;

/// <summary>
/// Configuration for AWS Bedrock Claude integration.
/// </summary>
public class BedrockConfig
{
    /// <summary>
    /// Enable/disable Bedrock AI responses. When disabled, falls back to canned responses.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// AWS region for Bedrock API calls.
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Claude model ID. Haiku is fast and cost-effective for chat.
    /// </summary>
    public string ModelId { get; set; } = "anthropic.claude-3-haiku-20240307-v1:0";

    /// <summary>
    /// Maximum tokens in response. Keep short for Twitch chat.
    /// </summary>
    public int MaxTokens { get; set; } = 150;

    /// <summary>
    /// Temperature for response creativity (0.0 = deterministic, 1.0 = creative).
    /// </summary>
    public double Temperature { get; set; } = 0.8;

    /// <summary>
    /// Timeout in seconds for Bedrock API calls.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Path to the QAI personality system prompt file.
    /// </summary>
    public string PersonalityPromptPath { get; set; } = "Documentation/QAI_Personality_System_Prompt.md";
}
