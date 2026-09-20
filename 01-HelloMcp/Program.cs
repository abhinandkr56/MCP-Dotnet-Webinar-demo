// ============================================================
// DEMO 1 — "Hello MCP"
// The smallest useful MCP server. You will TYPE this one live.
// ============================================================
// Setup :
//   dotnet new console -n HelloMcp
//   cd HelloMcp
//   dotnet add package ModelContextProtocol
//   dotnet add package Microsoft.Extensions.Hosting
// ============================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);

// CRITICAL: stdout carries the JSON-RPC protocol stream.
// Anything else written to stdout corrupts the protocol, so send logs to stderr.
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();


[McpServerToolType]
public static class WeatherTool
{
    // The [Description] attributes are NOT documentation for humans.
    // They are the prompt the model reads to decide whether to call this tool.
    [McpServerTool]
    [Description("Gets the current weather conditions for a named city.")]
    public static string GetWeather(
        [Description("The city name, for example 'Bengaluru' or 'Seattle'.")] string city)
    {
        // Hard-coded so the demo never depends on a network call.
        return $"It is 28C and hazy in {city}.";
    }
}
