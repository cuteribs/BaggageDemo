using BaggageDemo.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;

namespace BaggageDemo.FunctionApp;

public class OrderProcessingOrchestration
{
	private static readonly ActivitySource TraceSource = new("BaggageDemo.FunctionApp");

	private readonly ILogger<OrderProcessingOrchestration> _logger;

	public OrderProcessingOrchestration(ILogger<OrderProcessingOrchestration> logger)
	{
		_logger = logger;
	}

	[Function(nameof(OrderProcessingOrchestrator))]
	public async Task<string> OrderProcessingOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
	{
		var input = context.GetInput<WorkflowContext>();
		var propagationContext = Propagators.DefaultTextMapPropagator.Extract(
			default,
			input.MyContext,
			(props, key) => key == nameof(MyContext) ? [props] : null
		);
		Baggage.Current = propagationContext.Baggage;
		using var activity = TraceSource.StartActivity(
			nameof(OrderProcessingOrchestrator),
			ActivityKind.Consumer,
			propagationContext.ActivityContext
		);
		var myContext = MyContextHelper.GetBaggage();
		_logger.LogInformation(">> OrderProcessingOrchestrator. TenantId: {TenantId}, UserId: {UserId}, {TraceId}",
			myContext?.TenantId, myContext?.UserId, Activity.Current!.TraceId.ToString());
		await context.CallActivityAsync(nameof(Activity1), input);
		await context.CallActivityAsync(nameof(Activity2), input);
		return "Completed";
	}

	[Function(nameof(Activity1))]
	public Task Activity1([ActivityTrigger] WorkflowContext context)
	{
		var myContext = MyContextHelper.GetBaggage();
		_logger.LogInformation(">> Activity1. TenantId: {TenantId}, UserId: {UserId}, {TraceId}",
			myContext?.TenantId, myContext?.UserId, Activity.Current!.TraceId.ToString());
		return Task.CompletedTask;
	}

	[Function(nameof(Activity2))]
	public Task Activity2([ActivityTrigger] WorkflowContext context)
	{
		var myContext = MyContextHelper.GetBaggage();
		_logger.LogInformation(">> Activity2. TenantId: {TenantId}, UserId: {UserId}, {TraceId}",
			myContext?.TenantId, myContext?.UserId, Activity.Current!.TraceId.ToString());
		return Task.CompletedTask;
	}
}