using BaggageDemo.Common;
using BaggageDemo.WebApi;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.ConfigureOpenTelemetry();

// Add services to the container.
builder.Services.AddScoped<OrderService>();
builder.Services.AddHttpClient();

var app = builder.Build();

// Baggage demonstration endpoint
app.MapPost("/api/orders", async (CreateOrderRequest request, OrderService orderService) =>
{
    var result = await orderService.CreateOrderAsync(request);
    return Results.Ok(result);
})
.WithName("CreateOrder");

app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Service = "WebApi" }))
    .WithName("HealthCheck");

app.Run();
