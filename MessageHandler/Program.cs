using BaggageDemo.Common;
using BaggageDemo.MessageHandler;

var builder = Host.CreateApplicationBuilder(args);

// Add service defaults & Aspire client integrations.
builder.ConfigureOpenTelemetry();

builder.Services.AddHostedService<OrderMessageWorker>();

var host = builder.Build();
host.Run();
