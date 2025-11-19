using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
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
        using var activity = new Activity(nameof(ServiceBusQueueTrigger));
		var messageProperties = message.ApplicationProperties;

		if (messageProperties.TryGetValue("traceparent", out var val1) && val1 is string parentId)
		{
			activity.SetParentId(parentId);
		}

		if (messageProperties.TryGetValue("baggage", out var val2) && val2 is string baggageHeader)
		{
			ActivityHelper.Decode(activity, baggageHeader);
		}

		activity.Start();

		_logger.LogWarning("Run 2 ActivityId: {ActivityId}", Activity.Current?.Id);

		var input = new TaskInput
		{
			Payload = message.Body.ToString(),
			TraceParent = activity.Id,
			Baggage = ActivityHelper.Encode(activity.Baggage)
		};

		await client.ScheduleNewOrchestrationInstanceAsync(
			nameof(DurableFunctionsOrchestration),
			input
		);
        throw new NotImplementedException();
    }
}

public class DurableFunctionsOrchestration
{
	private readonly ILogger<DurableFunctionsOrchestration> _logger;

	public DurableFunctionsOrchestration(ILogger<DurableFunctionsOrchestration> logger)
	{
		_logger = logger;
	}

	[Function(nameof(DurableFunctionsOrchestration))]
	public async Task<List<string>> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context, FunctionContext executionContext)
	{
		var input = context.GetInput<TaskInput>()!;
		using var activity = ActivityHelper.StartNewActivity(nameof(DurableFunctionsOrchestration), input);

		_logger.LogWarning("RunOrchestrator ActivityId: {ActivityId}", Activity.Current?.Id);
		await InternalMethod();
		await context.CallActivityAsync(nameof(SayHello), input);
		await context.CallActivityAsync(nameof(SayHello), input);
		await context.CallActivityAsync(nameof(SayHello), input);
		await InternalMethod();

		return ["Hello Tokyo!", "Hello Seattle!", "Hello London!"];
	}

	[Function(nameof(SayHello))]
	public Task SayHello([ActivityTrigger] TaskInput input)
	{
		_logger.LogWarning(">> SayHello1: {Name}, ActivityId: {ActivityId}", 1, Activity.Current?.Id);
		using var activity = ActivityHelper.StartNewActivity(nameof(SayHello), input);
		_logger.LogWarning("<< SayHello2: {Name}, ActivityId: {ActivityId}", 2, Activity.Current?.Id);
		InternalMethod();
		return Task.CompletedTask;
	}

	private Task InternalMethod()
	{
		_logger.LogWarning("InternalMethod ActivityId: {ActivityId}", Activity.Current?.Id);
		return Task.CompletedTask;
	}
}

public class TraceContext
{
	public string? TraceParent { get; set; }
	public string? Baggage { get; set; }
}

public class TaskInput : TraceContext
{
	public string? Payload { get; set; }
}

public static class ActivityHelper
{
	public static Activity StartNewActivity(string name, TraceContext context)
	{
		var activity = new Activity(name);

		if (context.TraceParent != null)
		{
			activity.SetParentId(context.TraceParent);
		}

		if (context.Baggage != null)
		{
			Decode(activity, context.Baggage);
		}

		return activity.Start();
	}

	/// <summary>
	/// Encodes baggage into a W3C baggage header
	/// </summary>
	/// <param name="baggage"></param>
	/// <returns></returns>
	public static string? Encode(IEnumerable<KeyValuePair<string, string?>>? baggage)
	{
		if (baggage == null) return null;

		var parts = baggage.Select(x => 
			$"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value ?? "")}"
		);
		return string.Join(",", parts);
	}

	/// <summary>
	/// Decodes a W3C baggage header and sets it into the <see cref="Activity.Baggage" />
	/// </summary>
	/// <param name="activity"></param>
	/// <param name="baggageHeader"></param>
	public static void Decode(Activity activity, string baggageHeader)
	{
		if (activity == null) return;

		var baggage = Decode(baggageHeader);

		if (baggage != null)
		{
			foreach (var item in baggage)
			{
				activity.SetBaggage(item.Key, item.Value);
			}
		}
	}

	/// <summary>
	/// Decodes a W3C baggage header
	/// </summary>
	/// <param name="baggageHeader"></param>
	/// <returns></returns>
	public static IEnumerable<KeyValuePair<string, string?>>? Decode(string baggageHeader)
	{
		return baggageHeader?.Split(',', StringSplitOptions.RemoveEmptyEntries)
			.Where(x => x.Contains('='))
			.Select(x =>
			{
				var parts = x.Split('=', 2);
				var key = parts.First();
				var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : null;
				return new KeyValuePair<string, string?>(key, value);
			});
	}
}