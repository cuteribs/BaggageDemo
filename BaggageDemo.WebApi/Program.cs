using BaggageDemo.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddScoped<OrderService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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
