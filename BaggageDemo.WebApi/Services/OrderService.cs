using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BaggageDemo.Contracts;
using BaggageDemo.GrpcApi;
using Grpc.Net.Client;
using RabbitMQ.Client;

namespace BaggageDemo.WebApi.Services;

public class OrderService
{
    private readonly ILogger<OrderService> _logger;
    private readonly IConfiguration _configuration;

    public OrderService(ILogger<OrderService> logger, IConfiguration _configuration)
    {
        _logger = logger;
        this._configuration = _configuration;
    }

    public async Task<OrderResult> CreateOrderAsync(CreateOrderRequest request)
    {
        // Get or create activity for tracing
        var activity = Activity.Current ?? new Activity("CreateOrder").Start();

        // Set baggage items - these will propagate to downstream services
        activity.SetBaggage("tenant-id", request.TenantId);
        activity.SetBaggage("correlation-id", request.CorrelationId ?? Guid.NewGuid().ToString());
        activity.SetBaggage("user-id", request.UserId);

        var tenantId = activity.GetBaggageItem("tenant-id");
        var correlationId = activity.GetBaggageItem("correlation-id");
        var userId = activity.GetBaggageItem("user-id");

        _logger.LogInformation(
            "Creating order for customer {CustomerName} with amount {Amount}. " +
            "Baggage -> TenantId: {TenantId}, CorrelationId: {CorrelationId}, UserId: {UserId}",
            request.CustomerName, request.Amount,
            tenantId, correlationId, userId);

        var orderId = Guid.NewGuid().ToString();

        // Call gRPC service - baggage will be automatically propagated
        var grpcResult = await CallGrpcServiceAsync(orderId, request);

        // Publish message to RabbitMQ - we'll manually add baggage to message
        await PublishMessageAsync(orderId, request, tenantId!, correlationId!, userId!);

        return new OrderResult
        {
            OrderId = orderId,
            Status = "Created",
            GrpcResponse = grpcResult,
            TenantId = tenantId,
            CorrelationId = correlationId,
            UserId = userId
        };
    }

    private async Task<string> CallGrpcServiceAsync(string orderId, CreateOrderRequest request)
    {
        try
        {
            // In production, use HttpClientFactory and service discovery
            var grpcAddress = _configuration["GrpcApi:Address"] ?? "https://localhost:7214";
            
            using var channel = GrpcChannel.ForAddress(grpcAddress);
            var client = new BaggageProcessor.BaggageProcessorClient(channel);

            var grpcRequest = new OrderRequest
            {
                OrderId = orderId,
                CustomerName = request.CustomerName,
                Amount = (double)request.Amount
            };

            // Baggage is automatically propagated via gRPC metadata by OpenTelemetry
            var reply = await client.ProcessOrderAsync(grpcRequest);

            _logger.LogInformation(
                "gRPC response: {Message}, TenantId: {TenantId}, CorrelationId: {CorrelationId}",
                reply.Message, reply.TenantId, reply.CorrelationId);

            return reply.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling gRPC service");
            return $"gRPC call failed: {ex.Message}";
        }
    }

    private async Task PublishMessageAsync(
        string orderId, 
        CreateOrderRequest request, 
        string tenantId, 
        string correlationId, 
        string userId)
    {
        try
        {
            var rabbitHost = _configuration["RabbitMQ:Host"] ?? "localhost";
            var factory = new ConnectionFactory { HostName = rabbitHost };
            
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "orders",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var message = new OrderCreatedMessage
            {
                OrderId = orderId,
                CustomerName = request.CustomerName,
                Amount = request.Amount,
                CreatedAt = DateTime.UtcNow,
                // Include baggage in the message payload
                TenantId = tenantId,
                CorrelationId = correlationId,
                UserId = userId
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: "orders",
                body: body);

            _logger.LogInformation(
                "Published message for order {OrderId} with baggage TenantId: {TenantId}, CorrelationId: {CorrelationId}",
                orderId, tenantId, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to RabbitMQ");
        }
    }
}

public class CreateOrderRequest
{
    public required string CustomerName { get; init; }
    public decimal Amount { get; init; }
    public required string TenantId { get; init; }
    public string? CorrelationId { get; init; }
    public required string UserId { get; init; }
}

public class OrderResult
{
    public required string OrderId { get; init; }
    public required string Status { get; init; }
    public string? GrpcResponse { get; init; }
    public string? TenantId { get; init; }
    public string? CorrelationId { get; init; }
    public string? UserId { get; init; }
}
