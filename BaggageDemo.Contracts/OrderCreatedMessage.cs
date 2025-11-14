namespace BaggageDemo.Contracts;

public class OrderCreatedMessage
{
    public required string OrderId { get; init; }
    public required string CustomerName { get; init; }
    public decimal Amount { get; init; }
    public DateTime CreatedAt { get; init; }
    
    // Baggage fields for demonstration
    public string? TenantId { get; init; }
    public string? CorrelationId { get; init; }
    public string? UserId { get; init; }
}
