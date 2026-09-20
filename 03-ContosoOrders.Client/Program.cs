// ============================================================
// DEMO 3 — Consuming an MCP server from C#
// ============================================================
// Two modes, on purpose:
//
//   dotnet run              -> CHAT mode (default). Needs GROK_API_KEY set.
//                              Interactive chat; the model chooses the
//                              tools itself and remembers the conversation.
//
//   dotnet run -- raw       -> RAW mode.  No LLM, no API key, no network.
//                              Lists tools and calls one directly.
//                              Your fallback if the wifi misbehaves.
//
// Start the server (Demo 2) first: it must be listening on :3001.
// ============================================================

using System.ClientModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;

var useLlm = !(args.Length > 0 && args[0].Equals("raw", StringComparison.OrdinalIgnoreCase));

// ---------------------------------------------------------------------------
// 1. Connect to the MCP server over Streamable HTTP.
// ---------------------------------------------------------------------------
var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Name = "ContosoOrders",
    Endpoint = new Uri("http://localhost:3001/mcp"),
});

// Registering an elicitation handler tells the server "I can ask a human".
// Tools like refund_order use it to get approval before doing anything risky.
var clientOptions = new McpClientOptions
{
    Capabilities = new ClientCapabilities { Elicitation = new ElicitationCapability() },
    Handlers = new McpClientHandlers { ElicitationHandler = AskHumanAsync },
};

await using var client = await McpClient.CreateAsync(transport, clientOptions);

Console.WriteLine("Connected to the ContosoOrders MCP server.\n");

// ---------------------------------------------------------------------------
// 2. Discovery. This is the whole point of MCP — the client did not need to
//    know anything about this server ahead of time.
// ---------------------------------------------------------------------------
var tools = await client.ListToolsAsync();

Console.WriteLine($"Discovered {tools.Count} tools:");
foreach (var tool in tools)
{
    Console.WriteLine($"  - {tool.Name}: {tool.Description}");
}
Console.WriteLine();

if (!useLlm)
{
    // -----------------------------------------------------------------------
    // 3a. RAW MODE — call a tool directly, the way an LLM eventually will.
    // -----------------------------------------------------------------------
    Console.WriteLine("Calling search_orders for 'Aarav'...\n");

    var result = await client.CallToolAsync(
        "search_orders",
        new Dictionary<string, object?> { ["customerName"] = "Aarav" });

    foreach (var block in result.Content.OfType<TextContentBlock>())
    {
        Console.WriteLine(block.Text);
    }

    return;
}

// ---------------------------------------------------------------------------
// 3b. LLM MODE — hand the tools to a chat client and let the model drive.
//     McpClientTool derives from AIFunction, so this "just works" with any
//     IChatClient. No adapter, no glue code.
// ---------------------------------------------------------------------------
var apiKey = Environment.GetEnvironmentVariable("GROK_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("GROK_API_KEY is not set. Skipping LLM mode.");
    return;
}

IChatClient chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions()
    {
        Endpoint = new Uri("https://api.groq.com/openai/v1")
    })
    .GetChatClient("openai/gpt-oss-120b")
    .AsIChatClient()
    .AsBuilder()
    .UseFunctionInvocation()   // <- this is what actually executes the tool calls
    .Build();

// ---------------------------------------------------------------------------
// 4. The chat loop. The full history is sent every turn, so the model
//    remembers earlier questions ("what about his second order?").
// ---------------------------------------------------------------------------
const string SystemPrompt =
    "You are the Contoso Orders assistant. Use the available tools to look up " +
    "real order data instead of guessing. Keep answers short and clear.";

var chatOptions = new ChatOptions { Tools = [.. tools] };
List<ChatMessage> history = [new(ChatRole.System, SystemPrompt)];

Console.WriteLine("Chat with Contoso Orders. Type /clear to reset, or exit to quit.\n");

while (true)
{
    WriteColored("You: ", ConsoleColor.Cyan);
    var input = Console.ReadLine();

    if (input is null) break; // Ctrl+D / end of input
    input = input.Trim();
    if (input.Length == 0) continue;

    if (input is "exit" or "quit" or "/exit" or "/quit") break;
    if (input == "/clear")
    {
        history.RemoveRange(1, history.Count - 1);
        Console.WriteLine("(conversation cleared)\n");
        continue;
    }

    history.Add(new ChatMessage(ChatRole.User, input));

    WriteColored("Assistant: ", ConsoleColor.Green);
    List<ChatResponseUpdate> updates = [];
    try
    {
        await foreach (var update in chatClient.GetStreamingResponseAsync(history, chatOptions))
        {
            updates.Add(update);

            // Show when the model reaches for an MCP tool.
            foreach (var call in update.Contents.OfType<FunctionCallContent>())
            {
                WriteColored($"\n  [calling {call.Name}]\n", ConsoleColor.DarkGray);
            }

            Console.Write(update.Text);
        }

        history.AddMessages(updates);
    }
    catch (Exception ex)
    {
        // Drop the failed turn so the history stays valid for the next question.
        history.RemoveAt(history.Count - 1);
        WriteColored($"\n[error] {ex.Message}", ConsoleColor.Red);
    }

    Console.WriteLine("\n");
}

Console.WriteLine("Bye!");

// ---------------------------------------------------------------------------
// Human approval. The server calls this mid-tool-call (e.g. refund_order) and
// waits for the answer. We show its message, fill in each requested field
// from the keyboard, and send back accept / decline / cancel.
// ---------------------------------------------------------------------------
static ValueTask<ElicitResult> AskHumanAsync(ElicitRequestParams? request, CancellationToken cancellationToken)
{
    if (request is null)
    {
        return ValueTask.FromResult(new ElicitResult { Action = "cancel" });
    }

    WriteColored("\n\n  ⚠ Approval needed\n", ConsoleColor.Yellow);
    WriteColored($"  {request.Message}\n", ConsoleColor.Yellow);

    var content = new Dictionary<string, JsonElement>();
    var properties = request.RequestedSchema?.Properties
                     ?? new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>();

    foreach (var (name, schema) in properties)
    {
        var label = schema.Description ?? schema.Title ?? name;

        switch (schema)
        {
            case ElicitRequestParams.BooleanSchema:
                WriteColored($"  {label} [y = approve / n = decline]: ", ConsoleColor.Yellow);
                var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (answer is not ("y" or "yes"))
                {
                    WriteColored("  ✗ Declined\n\n", ConsoleColor.Red);
                    return ValueTask.FromResult(new ElicitResult { Action = "decline" });
                }
                content[name] = JsonSerializer.SerializeToElement(true);
                break;

            case ElicitRequestParams.NumberSchema:
                WriteColored($"  {label}: ", ConsoleColor.Yellow);
                if (!double.TryParse(Console.ReadLine(), out var number))
                {
                    WriteColored("  ✗ Not a number, cancelled\n\n", ConsoleColor.Red);
                    return ValueTask.FromResult(new ElicitResult { Action = "cancel" });
                }
                content[name] = JsonSerializer.SerializeToElement(number);
                break;

            case ElicitRequestParams.StringSchema:
                WriteColored($"  {label}: ", ConsoleColor.Yellow);
                content[name] = JsonSerializer.SerializeToElement(Console.ReadLine() ?? "");
                break;

            default:
                // Enums and other field types aren't supported by this console UI.
                WriteColored($"  ✗ Can't ask for '{name}' here, cancelled\n\n", ConsoleColor.Red);
                return ValueTask.FromResult(new ElicitResult { Action = "cancel" });
        }
    }

    WriteColored("  ✓ Approved\n\n", ConsoleColor.Green);
    return ValueTask.FromResult(new ElicitResult { Action = "accept", Content = content });
}

static void WriteColored(string text, ConsoleColor color)
{
    var previous = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.Write(text);
    Console.ForegroundColor = previous;
}
