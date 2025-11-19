using BaggageDemo.Common;
using BaggageDemo.WebApi;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.ConfigureOpenTelemetry();

// Add services to the container.
builder.Services.AddScoped<OrderService>();
builder.Services.AddHttpClient();

var app = builder.Build();

// Baggage demonstration endpoint
app.MapPost("/orders", async (CreateOrderRequest request, OrderService orderService) =>
{
    var result = await orderService.CreateOrderAsync(request);
    return result;
});

app.MapGet("/ping", () => Console.WriteLine($">> Ping {Activity.Current?.Id}"));

app.Run();
