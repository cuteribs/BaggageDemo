using BaggageDemo.Common;
using Grpc.Core;
using System.Diagnostics;

namespace BaggageDemo.GrpcApi;

public class BaggageProcessorService : BaggageProcessor.BaggageProcessorBase
{
    private readonly ILogger<BaggageProcessorService> _logger;

    public BaggageProcessorService(ILogger<BaggageProcessorService> logger)
    {
        _logger = logger;
    }

    public override Task<OrderReply> ProcessOrder(OrderRequest request, ServerCallContext context)
    {
        var myContext = MyContextHelper.GetBaggage()!;

        _logger.LogInformation(
            "Processing order {OrderId} for customer {CustomerName} with amount {Amount}. " +
            "Baggage -> TenantId: {TenantId}, CorrelationId: {CorrelationId}, UserId: {UserId}",
            request.OrderId, request.CustomerName, request.Amount,
			myContext.TenantId, myContext.CorrelationId, myContext.UserId);

        var reply = new OrderReply
        {
            Message = $"Order {request.OrderId} processed successfully",
            ProcessedBy = "GrpcApi"
        };

		_logger.LogError("GrpcApi {TraceId}", Activity.Current?.TraceId.ToString());
		return Task.FromResult(reply);
    }
}
