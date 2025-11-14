using BaggageDemo.GrpcApi;
using Grpc.Core;
using System.Diagnostics;

namespace BaggageDemo.GrpcApi.Services;

public class BaggageProcessorService : BaggageProcessor.BaggageProcessorBase
{
    private readonly ILogger<BaggageProcessorService> _logger;

    public BaggageProcessorService(ILogger<BaggageProcessorService> logger)
    {
        _logger = logger;
    }

    public override Task<OrderReply> ProcessOrder(OrderRequest request, ServerCallContext context)
    {
        // Extract baggage from current activity (propagated via gRPC headers)
        var activity = Activity.Current;
        
        var tenantId = activity?.GetBaggageItem("tenant-id") ?? "unknown";
        var correlationId = activity?.GetBaggageItem("correlation-id") ?? "unknown";
        var userId = activity?.GetBaggageItem("user-id") ?? "unknown";

        _logger.LogInformation(
            "Processing order {OrderId} for customer {CustomerName} with amount {Amount}. " +
            "Baggage -> TenantId: {TenantId}, CorrelationId: {CorrelationId}, UserId: {UserId}",
            request.OrderId, request.CustomerName, request.Amount,
            tenantId, correlationId, userId);

        var reply = new OrderReply
        {
            Message = $"Order {request.OrderId} processed successfully",
            ProcessedBy = "GrpcApi",
            TenantId = tenantId,
            CorrelationId = correlationId,
            UserId = userId
        };

        return Task.FromResult(reply);
    }
}
