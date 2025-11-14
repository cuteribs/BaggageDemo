namespace BaggageDemo.Contracts;

public class OrderCreatedMessage
{
    public required string OrderId { get; init; }
    public required string CustomerName { get; init; }
    public decimal Amount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class MyContext
{
    public string? TenantId { get; set; }
    public string? CorrelationId { get; set; }
    public string? UserId { get; set; }
}