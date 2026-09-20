using System.ComponentModel;
using ContosoOrders.Server.Services;
using ModelContextProtocol.Server;

namespace ContosoOrders.Server.Tools;

// ---------------------------------------------------------------------------
// PROMPTS — reusable, parameterised instructions the USER picks from a menu.
// In VS Code these show up as slash commands. Discovery works exactly like
// tools: [McpServerPromptType] + [McpServerPrompt], found by WithPromptsFromAssembly().
// ---------------------------------------------------------------------------
[McpServerPromptType]
public static class OrderPrompts
{
    [McpServerPrompt(Name = "triage_customer")]
    [Description("Produces a support-triage briefing for one customer.")]
    public static string TriageCustomer(
        [Description("The customer's name.")] string customerName)
    {
        return $"""
                You are a customer support lead reviewing the account for {customerName}.

                1. Search their orders.
                2. Flag anything cancelled or stuck in Processing for more than a week.
                3. Summarise their total spend.
                4. Recommend one concrete next action.

                Be concise. Use bullet points.
                """;
    }
}

// ---------------------------------------------------------------------------
// RESOURCES — read-only context the client can attach, addressed by URI.
// Think "files the model can read", not "actions the model can take".
// ---------------------------------------------------------------------------
[McpServerResourceType]
public static class OrderResources
{
    [McpServerResource(UriTemplate = "contoso://policy/refunds", Name = "Refund policy")]
    [Description("The official Contoso refund policy that agents must follow.")]
    public static string RefundPolicy() =>
        """
        CONTOSO REFUND POLICY
        - Delivered orders may be refunded within 30 days of delivery.
        - Cancelled orders are refunded automatically within 5 business days.
        - Orders above Rs.25,000 require a team lead's approval.
        - Shipped orders must be returned before a refund is issued.
        """;
}
