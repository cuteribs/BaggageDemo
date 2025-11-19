using BaggageDemo.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using System.Diagnostics;
using System.Text.Json;

namespace BaggageDemo.FunctionApp;

public class RabbitMQTriggerFunction
{
	//[Function(nameof(RabbitMQTriggerFunction))]
	//public async Task Run(
	//	[RabbitMQTrigger("orders", ConnectionStringSetting = "RabbitMQConnection")] string messageBody,
	//	[DurableClient] DurableTaskClient client,
	//	FunctionContext functionContext
	//)
	//{
	//	var logger = functionContext.GetLogger<RabbitMQTriggerFunction>();
	//	var myContext = MyContextHelper.GetBaggage();
	//	var input = new WorkflowContext
	//	{
	//		Message = JsonSerializer.Deserialize<OrderCreatedMessage>(messageBody),
	//		MyContext = Baggage.GetBaggage(nameof(MyContext))
	//	};
	//	var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
	//		nameof(OrderProcessingOrchestration),
	//		input
	//	);
	//	logger.LogInformation(">> RabbitMQTriggerFunction. TenantId: {TenantId}, UserId: {UserId}, TraceId: {TraceId}",
	//		myContext?.TenantId, myContext?.UserId, Activity.Current!.TraceId.ToString());
	//}
}