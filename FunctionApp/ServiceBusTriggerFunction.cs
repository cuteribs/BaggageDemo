using Azure.Messaging.ServiceBus;
using BaggageDemo.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;
using System.Text.Json;

namespace BaggageDemo.FunctionApp;

public class ServiceBusTriggerFunction
{
	private static readonly ActivitySource TraceSource = new("BaggageDemo.FunctionApp");

	[Function(nameof(ServiceBusTriggerFunction))]
	public async Task Run(
		[ServiceBusTrigger("orders", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage message,
		[DurableClient] DurableTaskClient client,
		FunctionContext context
	)
	{
		using var activity = new Activity(nameof(ServiceBusTriggerFunction));

		if (message.ApplicationProperties.TryGetValue("traceparent", out var value) && value is string parentId)
		{
			activity.SetParentId(parentId);
		}

		activity.Start();
		var logger = context.GetLogger<ServiceBusTriggerFunction>();

		var propagationContext = Propagators.DefaultTextMapPropagator.Extract(
			default,
			message.ApplicationProperties,
			(props, key) => props.TryGetValue(key, out var value) && value is string str ? [str] : null
		);
		Baggage.Current = propagationContext.Baggage;
		var myContext = MyContextHelper.GetBaggage();
		var input = new WorkflowContext
		{
			Message = JsonSerializer.Deserialize<OrderCreatedMessage>(message.Body.ToString()),
			MyContext = Baggage.GetBaggage(nameof(MyContext))
		};
		var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
			"OrderProcessingOrchestrator",
			input
		);
		logger.LogInformation(">> ServiceBusTriggerFunction. TenantId: {TenantId}, UserId: {UserId}, TraceId: {TraceId}",
			myContext?.TenantId, myContext?.UserId, Activity.Current?.TraceId.ToString());
	}
}