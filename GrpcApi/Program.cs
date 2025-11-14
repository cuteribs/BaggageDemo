using BaggageDemo.Common;
using BaggageDemo.GrpcApi;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.ConfigureOpenTelemetry();

// Add services to the container.
builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<BaggageProcessorService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();
