using System.ComponentModel;
using System.Text.Json;
using ContosoOrders.Server.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ContosoOrders.Server.Tools;

// ============================================================================
//  BONUS / STRETCH DEMO — Elicitation (human in the loop)
// ----------------------------------------------------------------------------
//  1. Elicitation is a SERVER-TO-CLIENT request. That means the server needs a
//     session, so it does NOT work in stateless mode. In SDK v2, stateless is
//     the DEFAULT — you must explicitly set options.Stateless = false.
//     (See Program.cs, where this is switched off for the demo.)
//
//  2. The client must support elicitation. MCP Inspector does. Not every
//     client does. If the client has no elicitation capability, this tool
//     returns a clear message rather than hanging.
//
//  3. This is the most version-sensitive API in the whole demo set. BUILD AND
//     RUN THIS ONE FIRST when you rehearse. If it fights you, just delete this
//     file — demos 1, 2 and 3 do not depend on it, and the webinar works fine
//     without it. Do not burn stage time debugging this live.
// ============================================================================

[McpServerToolType]
public class RefundTools
{
    private readonly OrderStore _store;

    public RefundTools(OrderStore store) => _store = store;

    [McpServerTool(Name = "refund_order")]
    [Description("Refunds a customer order. This is irreversible and will ask the " +
                 "human operator to confirm before the refund is applied.")]
    public async Task<string> RefundOrder(
        McpServer server,
        [Description("The exact order ID to refund, for example 'ORD-1002'.")] string orderId,
        CancellationToken cancellationToken = default)
    {
        var order = _store.GetById(orderId);
        if (order is null)
        {
            return $"No order found with ID '{orderId}'.";
        }

        // If the client can't show a confirmation prompt, refuse rather than
        // silently refunding money on the user's behalf.
        if (server.ClientCapabilities?.Elicitation is null)
        {
            return "This client does not support confirmation prompts, so the refund " +
                   "was not applied. Refunds require human confirmation.";
        }

        var result = await server.ElicitAsync(
            new ElicitRequestParams
            {
                Message = $"Refund {order.OrderId} for {order.CustomerName}, " +
                          $"amount Rs.{order.Total:N2}? This cannot be undone.",
                RequestedSchema = new ElicitRequestParams.RequestSchema
                {
                    Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                    {
                        ["confirm"] = new ElicitRequestParams.BooleanSchema
                        {
                            Description = "Tick to approve this refund."
                        }
                    }
                }
            },
            cancellationToken);

        // The user can accept, decline, or cancel. Only "accept" plus an
        // explicit true means go ahead.
        var confirmed =
            result.Action == "accept" &&
            result.Content is not null &&
            result.Content.TryGetValue("confirm", out var value) &&
            value.ValueKind == JsonValueKind.True;

        if (!confirmed)
        {
            return $"Refund for {order.OrderId} was NOT applied. The operator declined.";
        }

        _store.MarkRefunded(order.OrderId);
        return $"Refund applied. {order.OrderId} is now marked Refunded.";
    }
}
