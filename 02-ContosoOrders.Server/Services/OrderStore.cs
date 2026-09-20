namespace ContosoOrders.Server.Services;

public record Order(
    string OrderId,
    string CustomerName,
    string Status,
    decimal Total,
    DateOnly PlacedOn,
    string[] Items);

/// <summary>
/// Stands in for "your real system" — a database, a REST API, a legacy service.
/// The point of the demo is that MCP does not care what is behind this.
/// </summary>
public class OrderStore
{
    private readonly List<Order> _orders =
    [
        new("ORD-1001", "Aarav Sharma",   "Delivered",  4599.00m, new DateOnly(2026, 8, 14), ["Mechanical Keyboard", "USB-C Hub"]),
        new("ORD-1002", "Priya Nair",     "Shipped",   12750.50m, new DateOnly(2026, 8, 29), ["27in Monitor"]),
        new("ORD-1003", "Rahul Verma",    "Processing", 899.00m,  new DateOnly(2026, 9, 2),  ["Laptop Stand"]),
        new("ORD-1004", "Aarav Sharma",   "Cancelled", 2199.00m,  new DateOnly(2026, 7, 30), ["Webcam"]),
        new("ORD-1005", "Sneha Iyer",     "Delivered", 34999.00m, new DateOnly(2026, 6, 11), ["Ergonomic Chair"]),
    ];

    public IEnumerable<Order> Search(string? customer, string? status)
    {
        IEnumerable<Order> query = _orders;

        if (!string.IsNullOrWhiteSpace(customer))
        {
            query = query.Where(o =>
                o.CustomerName.Contains(customer, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o =>
                o.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    public Order? GetById(string orderId) =>
        _orders.FirstOrDefault(o =>
            o.OrderId.Equals(orderId, StringComparison.OrdinalIgnoreCase));

    public bool MarkRefunded(string orderId)
    {
        var index = _orders.FindIndex(o =>
            o.OrderId.Equals(orderId, StringComparison.OrdinalIgnoreCase));

        if (index < 0) return false;

        _orders[index] = _orders[index] with { Status = "Refunded" };
        return true;
    }
}
