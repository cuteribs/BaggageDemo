using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddLogging(b =>
    {
        b.AddSimpleConsole(o => o.ColorBehavior = LoggerColorBehavior.Enabled)
            .SetMinimumLevel(LogLevel.Warning);
    }
);

builder.Services.AddOpenTelemetry()
    .UseFunctionsWorkerDefaults();

builder.Build().Run();
