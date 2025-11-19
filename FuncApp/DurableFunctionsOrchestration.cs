using BaggageDemo.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BaggageDemo.FuncApp;

public class DurableFunctionsOrchestration
{
	private readonly ILogger<DurableFunctionsOrchestration> _logger;
	private readonly IHttpClientFactory _httpClientFactory;

	public DurableFunctionsOrchestration(ILogger<DurableFunctionsOrchestration> logger, IHttpClientFactory httpClientFactory)
	{
		_logger = logger;
		_httpClientFactory = httpClientFactory;
	}

	[Function(nameof(DurableFunctionsOrchestration))]
	public async Task<List<string>> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context, FunctionContext executionContext)
	{
		var input = context.GetInput<TaskInput>()!;
		using var activity = ActivityHelper.StartNewActivity(nameof(DurableFunctionsOrchestration), input);

		_logger.LogWarning("RunOrchestrator ActivityId: {ActivityId}", Activity.Current?.Id);
		await context.CallActivityAsync(nameof(SayHello), input);
		await context.CallActivityAsync(nameof(SayHello), input);
		await context.CallActivityAsync(nameof(SayHello), input);

		return ["Hello Tokyo!", "Hello Seattle!", "Hello London!"];
	}

	[Function(nameof(SayHello))]
	public async Task SayHello([ActivityTrigger] TaskInput input)
	{
		_logger.LogWarning(">> SayHello1: {Name}, ActivityId: {ActivityId}", 1, Activity.Current?.Id);
		using var activity = ActivityHelper.StartNewActivity(nameof(SayHello), input);
		_logger.LogWarning("<< SayHello2: {Name}, ActivityId: {ActivityId}", 2, Activity.Current?.Id);
		await InternalMethod();
	}

	private async Task InternalMethod()
	{
		_logger.LogWarning("InternalMethod ActivityId: {ActivityId}", Activity.Current?.Id);
		var client = _httpClientFactory.CreateClient();
		await client.GetAsync("http://localhost:5000/ping");
	}
}

public class TaskInput : MessageTraceContext
{
	public string? Payload { get; set; }
}