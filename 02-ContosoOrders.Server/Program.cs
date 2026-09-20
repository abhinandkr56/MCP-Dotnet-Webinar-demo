// ============================================================
// DEMO 2 — "ContosoOrders" : a real MCP server over HTTP
// Pre-built. You walk through this one, you do not type it.
// ============================================================
// Setup:
//   dotnet new web -n ContosoOrders.Server
//   dotnet add package ModelContextProtocol.AspNetCore
// Run:
//   dotnet run
//   -> listens on http://localhost:3001/mcp
// ============================================================

using ContosoOrders.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Your ordinary application services. Nothing here knows what MCP is.
builder.Services.AddSingleton<OrderStore>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        // ------------------------------------------------------------------
        // THE ONE SETTING WORTH EXPLAINING ON STAGE:
        //
        // Since SDK v2, Stateless defaults to TRUE. Stateless servers scale
        // horizontally and need no sticky sessions — that is what you want in
        // production for a plain request/response tool server.
        //
        // But stateless servers cannot make server-to-client requests, and
        // elicitation IS a server-to-client request. So the refund demo needs
        // sessions. That is the trade-off, and it is worth naming out loud.
        //
        // If you drop RefundTools.cs, delete this line and take the v2 default.
        // ------------------------------------------------------------------
        options.Stateless = false;
    })
    .WithToolsFromAssembly()
    .WithPromptsFromAssembly()
    .WithResourcesFromAssembly();

var app = builder.Build();

// Maps the Streamable HTTP endpoint. The pattern is optional, but naming it
// explicitly makes it obvious in logs and reverse-proxy rules.
app.MapMcp("/mcp");

app.Run("http://localhost:3001");
