using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddLogging(x => x.AddSimpleConsole().SetMinimumLevel(LogLevel.Warning));

//builder.Services.AddApplicationInsightsTelemetryWorkerService()
//	.ConfigureFunctionsApplicationInsights();

builder.AddW3CTracing(o =>
{
	o.TracingExtractors.Add(W3CTracingOptions.ExtractServiceBusTracing);
	o.TracingExtractors.Add(W3CTracingOptions.ExtractRabbitMQTracing);
});

builder.Build().Run();


public class W3CTracingMiddleware : IFunctionsWorkerMiddleware
{
	private static readonly ActivitySource Source = new(W3CTracingInitializer.SourceName);

	private readonly W3CTracingOptions _options;

	public W3CTracingMiddleware(IOptions<W3CTracingOptions> options)
	{
		_options = options.Value;
	}

	public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
	{
		var traceContext = _options.TracingExtractors
			.Select(x => x(context))
			.FirstOrDefault(x => x != null);

		var activity = Source.StartActivity(
			nameof(W3CTracingMiddleware),
			ActivityKind.Consumer,
			traceContext?.TraceParent
		);

		if (activity != null && traceContext != null)
		{
			if (!string.IsNullOrWhiteSpace(traceContext.TraceState))
			{
				activity.TraceStateString = traceContext.TraceState;
			}

			if (!string.IsNullOrWhiteSpace(traceContext.Baggage))
			{
				foreach (var item in traceContext.Baggage.Split(','))
				{
					var parts = item.Split('=', 2);

					if (parts.Length == 2)
					{
						activity.AddBaggage(parts[0], Uri.UnescapeDataString(parts[1]));
					}
				}
			}
		}

		try
		{
			await next(context);
		}
		finally
		{
			activity?.Dispose();
		}
	}
}

public record W3CTracingContext(string? TraceParent, string? TraceState, string? Baggage);

public class W3CTracingOptions
{
	public ICollection<Func<FunctionContext, W3CTracingContext?>> TracingExtractors { get; set; } = [];

	public static W3CTracingContext? ExtractServiceBusTracing(FunctionContext context)
	{
		if (context.BindingContext.BindingData.TryGetValue("ApplicationProperties", out var value) && value is string str)
		{
			var json = JsonDocument.Parse(str).RootElement;
			var traceParent = GetStringProperty(json, "traceparent");
			var traceState = GetStringProperty(json, "tracestate");
			var baggage = GetStringProperty(json, "baggage");
			return new(traceParent, traceState, baggage);
		}

		return null;
	}

	public static W3CTracingContext? ExtractRabbitMQTracing(FunctionContext context)
	{
		if (context.BindingContext.BindingData.TryGetValue("BasicProperties", out var value) && value is string str)
		{
			var json = JsonDocument.Parse(str);

			if (json.RootElement.TryGetProperty("Headers", out var headers))
			{
				var traceParent = DecodeBase64(GetStringProperty(headers, "traceparent"));
				var traceState = DecodeBase64(GetStringProperty(headers, "tracestate"));
				var baggage = DecodeBase64(GetStringProperty(headers, "baggage"));
				return new(traceParent, traceState, baggage);
			}
		}

		return null;
	}

	private static string? GetStringProperty(JsonElement json, string propertyName)
	{
		if (json.TryGetProperty(propertyName, out var property))
		{
			return property.GetString();
		}

		return null;
	}

	private static string? DecodeBase64(string? base64String)
	{
		if (base64String == null) return null;

		return Encoding.UTF8.GetString(Convert.FromBase64String(base64String));
	}
}

public class W3CTracingInitializer : IHostedService
{
	public const string SourceName = "W3CTracing";

	public Task StartAsync(CancellationToken cancellationToken)
	{
		ActivitySource.AddActivityListener(new()
		{
			ShouldListenTo = x => x.Name == SourceName,
			Sample = new((ref _) => ActivitySamplingResult.AllData)
		});

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}

public static class W3CTracingMiddlewareExtensions
{
	public static IFunctionsWorkerApplicationBuilder AddW3CTracing(
		this IFunctionsWorkerApplicationBuilder builder,
		Action<W3CTracingOptions> configureOptions
	)
	{
		ArgumentNullException.ThrowIfNull(configureOptions);

		builder.Services.Configure<W3CTracingOptions>(configureOptions);
		builder.Services.AddSingleton<IHostedService, W3CTracingInitializer>();

		builder.UseMiddleware<W3CTracingMiddleware>();

		return builder;
	}
}