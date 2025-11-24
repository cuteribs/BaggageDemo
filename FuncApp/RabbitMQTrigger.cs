using BaggageDemo.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace BaggageDemo.FuncApp;

public class RabbitMQTrigger
{
	private readonly ILogger<RabbitMQTrigger> _logger;

	public RabbitMQTrigger(ILogger<RabbitMQTrigger> logger)
	{
		_logger = logger;
	}

	[Function(nameof(RabbitMQTrigger))]
	public async Task Run(
		[RabbitMQTrigger("orders", ConnectionStringSetting = "ConnectionStrings:RabbitMQ")] string message,
		[DurableClient] DurableTaskClient client,
		FunctionContext functionContext
	)
	{
		//_logger.LogWarning("Run 1 ActivityId: {ActivityId}", Activity.Current?.Id);

		//using var activity = new Activity(nameof(RabbitMQTrigger));

		//if (functionContext.BindingContext.BindingData.TryGetValue("BasicProperties", out var value) && value is string str)
		//{
		//	var json = JsonDocument.Parse(str);
		//	var headers = json.RootElement.GetProperty("Headers");
		//	var traceParent = headers.GetProperty("traceparent").GetString()?.FromBase64();
		//	var baggage = headers.GetProperty("baggage").GetString()?.FromBase64();
		//	var properties = new Dictionary<string, object?>();

		//	if (traceParent != null)
		//	{
		//		properties.Add("traceparent", traceParent);
		//	}

		//	if(baggage != null)
		//	{
		//		properties.Add("baggage", baggage);
		//	}

		//	ActivityHelper.Extract(properties, activity);
		//}

		//activity.Start();
		var activity = Activity.Current;
		_logger.LogWarning("Run 2 ActivityId: {ActivityId}", activity?.Id);

		var input = new TaskInput
		{
			Payload = message,
			TraceParent = activity?.Id,
			Baggage = ActivityHelper.Encode(activity?.Baggage)
		};

		await client.ScheduleNewOrchestrationInstanceAsync(
			nameof(DurableFunctionsOrchestration),
			input
		);
	}
}
