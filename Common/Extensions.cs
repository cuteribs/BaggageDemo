using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

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
}
