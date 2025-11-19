using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace BaggageDemo.Common;

public static class Extensions
{
	public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
	{
		builder.Logging.AddOpenTelemetry(x =>
		{
			x.IncludeFormattedMessage = true;
			x.IncludeScopes = true;
		});

		builder.Services.AddOpenTelemetry()
			.WithTracing(x => x.AddSource(builder.Environment.ApplicationName)
				.AddAspNetCoreInstrumentation()
				.AddGrpcClientInstrumentation()
				.AddHttpClientInstrumentation()
				.AddConsoleExporter()
			);

		return builder;
	}

	public static void Inject(this TextMapPropagator propagator, ServiceBusMessage message)
	{
		var activity = Activity.Current!;
		message.ApplicationProperties["Diagnostic-Id"] = activity.Id;

		propagator.Inject(
			new PropagationContext(activity.Context, Baggage.Current),
			message.ApplicationProperties,
			(props, key, value) => props[key] = value
		);
	}
}