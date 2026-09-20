using System.ComponentModel;
using ContosoOrders.Server.Services;
using ModelContextProtocol.Server;

namespace ContosoOrders.Server.Tools;

[McpServerToolType]
public class OrderTools
{
    // NOTE: OrderStore is injected by the normal ASP.NET Core DI container.
    // The SDK resolves constructor dependencies for you — nothing MCP-specific here.
    private readonly OrderStore _store;
    private readonly ILogger<OrderTools> _logger;

    public OrderTools(OrderStore store, ILogger<OrderTools> logger)
    {
        _store = store;
        _logger = logger;
    }

    [McpServerTool(Name = "search_orders")]
    [Description("Searches customer orders. Use this when the user asks about orders " +
                 "belonging to a person, or wants a list of orders in a particular state. " +
                 "Returns a compact summary line per matching order.")]
    public string SearchOrders(
        [Description("Full or partial customer name. Omit to search all customers.")]
        string? customerName = null,

        [Description("Order status filter. One of: Processing, Shipped, Delivered, Cancelled, Refunded.")]
        string? status = null)
    {
        _logger.LogInformation("search_orders called: customer={Customer} status={Status}",
            customerName, status);

        var results = _store.Search(customerName, status).ToList();

        if (results.Count == 0)
        {
            // Returning a plain sentence beats returning an empty array — the model
            // can act on "no results" but often stalls on "[]".
            return "No orders matched that search.";
        }

        return string.Join("\n", results.Select(o =>
            $"{o.OrderId} | {o.CustomerName} | {o.Status} | Rs.{o.Total:N2} | placed {o.PlacedOn:yyyy-MM-dd}"));
    }

    [McpServerTool(Name = "get_order_details")]
    [Description("Gets the full detail of one specific order, including the line items. " +
                 "Requires an exact order ID such as ORD-1001.")]
    public string GetOrderDetails(
        [Description("The exact order ID, for example 'ORD-1001'.")] string orderId)
    {
        var order = _store.GetById(orderId);

        if (order is null)
        {
            // Tell the model HOW to recover, not just that it failed.
            return $"No order found with ID '{orderId}'. " +
                   "Use search_orders to find the correct ID first.";
        }

        return $"""
                Order:     {order.OrderId}
                Customer:  {order.CustomerName}
                Status:    {order.Status}
                Total:     Rs.{order.Total:N2}
                Placed on: {order.PlacedOn:yyyy-MM-dd}
                Items:     {string.Join(", ", order.Items)}
                """;
    }
}
