using System.Text.Json;
using System.Text.Json.Serialization;

namespace SYSTEM.HeadlessClient.Mcp;

/// <summary>
/// JSON-RPC 2.0 Request for MCP protocol.
/// </summary>
public class McpRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }

    [JsonPropertyName("id")]
    public object? Id { get; set; }
}

/// <summary>
/// JSON-RPC 2.0 Response for MCP protocol.
/// </summary>
public class McpResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; } = "2.0";

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpError? Error { get; set; }

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    public static McpResponse Success(object? result, object? id) => new()
    {
        Result = result,
        Id = id
    };

    public static McpResponse Failure(int code, string message, object? id) => new()
    {
        Error = new McpError { Code = code, Message = message },
        Id = id
    };
}

/// <summary>
/// JSON-RPC 2.0 Error object.
/// </summary>
public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    // Standard JSON-RPC error codes
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
}

/// <summary>
/// MCP Initialize request parameters.
/// </summary>
public class McpInitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "";

    [JsonPropertyName("capabilities")]
    public McpClientCapabilities? Capabilities { get; set; }

    [JsonPropertyName("clientInfo")]
    public McpClientInfo? ClientInfo { get; set; }
}

/// <summary>
/// MCP client capabilities.
/// </summary>
public class McpClientCapabilities
{
    [JsonPropertyName("roots")]
    public McpRootsCapability? Roots { get; set; }

    [JsonPropertyName("sampling")]
    public object? Sampling { get; set; }
}

/// <summary>
/// MCP roots capability.
/// </summary>
public class McpRootsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// MCP client info.
/// </summary>
public class McpClientInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}

/// <summary>
/// MCP Initialize result.
/// </summary>
public class McpInitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = "2024-11-05";

    [JsonPropertyName("capabilities")]
    public McpServerCapabilities Capabilities { get; set; } = new();

    [JsonPropertyName("serverInfo")]
    public McpServerInfo ServerInfo { get; set; } = new();
}

/// <summary>
/// MCP server capabilities.
/// </summary>
public class McpServerCapabilities
{
    [JsonPropertyName("tools")]
    public McpToolsCapability? Tools { get; set; }

    [JsonPropertyName("resources")]
    public McpResourcesCapability? Resources { get; set; }
}

/// <summary>
/// MCP tools capability.
/// </summary>
public class McpToolsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// MCP resources capability.
/// </summary>
public class McpResourcesCapability
{
    [JsonPropertyName("subscribe")]
    public bool Subscribe { get; set; }

    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; set; }
}

/// <summary>
/// MCP server info.
/// </summary>
public class McpServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "qai-agent";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";
}

/// <summary>
/// MCP Tool definition.
/// </summary>
public class McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("inputSchema")]
    public McpInputSchema InputSchema { get; set; } = new();
}

/// <summary>
/// MCP Input schema for tool parameters.
/// </summary>
public class McpInputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, McpPropertySchema> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Required { get; set; }
}

/// <summary>
/// MCP Property schema.
/// </summary>
public class McpPropertySchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Enum { get; set; }
}

/// <summary>
/// MCP Resource definition.
/// </summary>
public class McpResource
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "application/json";
}

/// <summary>
/// MCP Resource contents.
/// </summary>
public class McpResourceContents
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "application/json";

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("blob")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Blob { get; set; }
}

/// <summary>
/// MCP Tool call parameters.
/// </summary>
public class McpToolCallParams
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; set; }
}

/// <summary>
/// MCP Tool result.
/// </summary>
public class McpToolResult
{
    [JsonPropertyName("content")]
    public List<McpToolContent> Content { get; set; } = new();

    [JsonPropertyName("isError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsError { get; set; }
}

/// <summary>
/// MCP Tool content item.
/// </summary>
public class McpToolContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

/// <summary>
/// MCP Read resource params.
/// </summary>
public class McpReadResourceParams
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";
}
