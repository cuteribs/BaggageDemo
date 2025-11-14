using BaggageDemo.MessageHandler;

var builder = Host.CreateApplicationBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.Services.AddHostedService<OrderMessageWorker>();

var host = builder.Build();
host.Run();
