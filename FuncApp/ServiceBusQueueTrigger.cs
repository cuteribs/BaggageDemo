using Azure.Messaging.ServiceBus;
using BaggageDemo.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BaggageDemo.FuncApp;

public class ServiceBusQueueTrigger
{
	private readonly ILogger<ServiceBusQueueTrigger> _logger;

	public ServiceBusQueueTrigger(ILogger<ServiceBusQueueTrigger> logger)
	{
		_logger = logger;
	}

	[Function(nameof(ServiceBusQueueTrigger))]
	public async Task Run(
		[ServiceBusTrigger("orders", Connection = "ConnectionStrings:ServiceBus")] ServiceBusReceivedMessage message,
		[DurableClient] DurableTaskClient client
	)
	{
		_logger.LogWarning("Run 1 ActivityId: {ActivityId}", Activity.Current?.Id);
		//using var activity = new Activity(nameof(ServiceBusQueueTrigger));
		//ActivityHelper.Extract(message.ApplicationProperties, activity);

		//activity.Start();

		_logger.LogWarning("Run 2 ActivityId: {ActivityId}", Activity.Current?.Id);

		var input = new TaskInput
		{
			Payload = message.Body.ToString(),
			//TraceParent = activity.Id,
			//Baggage = ActivityHelper.Encode(activity.Baggage)
		};

		await client.ScheduleNewOrchestrationInstanceAsync(
			nameof(DurableFunctionsOrchestration),
			input
		);
	}
}
