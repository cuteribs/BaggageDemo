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
		var myContext = ActivityHelper.GetBaggage<MyContext>(nameof(MyContext));
		_logger.LogInformation(
			"-- GrpcApi -- Processing order {OrderId} for customer {CustomerName} with amount {Amount}. " +
			"Baggage -> TenantId: {TenantId}, UserId: {UserId}, TraceId {TraceId}",
			request.OrderId,
			request.CustomerName,
			request.Amount,
			myContext.TenantId,
			myContext.UserId,
			Activity.Current!.TraceId.ToString()
		);

		var reply = new OrderReply
		{
			Message = $"Order {request.OrderId} processed successfully",
			ProcessedBy = "GrpcApi"
		};
		return Task.FromResult(reply);
	}
}
