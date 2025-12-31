using Microsoft.Extensions.Configuration;
using SYSTEM.HeadlessClient;
using SYSTEM.HeadlessClient.Config;

// Build configuration
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var configBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false);

// Only load environment-specific config if DOTNET_ENVIRONMENT is explicitly set
if (!string.IsNullOrEmpty(environment))
{
    configBuilder.AddJsonFile($"appsettings.{environment}.json", optional: true);
}

var config = configBuilder
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var clientConfig = new ClientConfig();
config.Bind(clientConfig);

// Override from command line for convenience
if (args.Length >= 2)
{
    clientConfig.QAI.Username = args[0];
    clientConfig.QAI.Pin = args[1];
}
if (args.Length >= 3)
{
    clientConfig.QAI.DisplayName = args[2];
}

// Validate config
if (string.IsNullOrEmpty(clientConfig.QAI.Username) || string.IsNullOrEmpty(clientConfig.QAI.Pin))
{
    Console.WriteLine("Usage: SYSTEM.QAI <username> <pin> [display_name]");
    Console.WriteLine("  Or set QAI__USERNAME and QAI__PIN environment variables");
    Console.WriteLine("  Or configure in appsettings.json");
    return 1;
}

// Create and run client
var client = new HeadlessClient(clientConfig);

// Handle Ctrl+C
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    client.Stop();
};

try
{
    await client.RunAsync();
    return 0;
}
catch (OperationCanceledException)
{
    // Normal shutdown
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}");
    return 1;
}
