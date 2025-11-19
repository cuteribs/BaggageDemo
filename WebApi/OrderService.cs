using Azure.Messaging.ServiceBus;
using BaggageDemo.Common;
using BaggageDemo.GrpcApi;
using Grpc.Net.Client;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BaggageDemo.WebApi;

public class OrderService
{
	private readonly ILogger<OrderService> _logger;
	private readonly IConfiguration _configuration;

	public OrderService(ILogger<OrderService> logger, IConfiguration configuration)
	{
		_logger = logger;
		_configuration = configuration;
	}

	public async Task<OrderResult> CreateOrderAsync(CreateOrderRequest request)
	{
		var myContext = MyContextHelper.GetBaggage();

		if (myContext == null)
		{
			myContext = new MyContext
			{
				TenantId = "Tenant1",
				UserId = "User1"
			};
			MyContextHelper.SetBaggage(myContext);
			Baggage.SetBaggage("Custom", JsonSerializer.Serialize(new { Info = "Some custom baggage data" }));
		}

		var orderId = Guid.NewGuid().ToString();
		//var grpcResult = await CallGrpcServiceAsync(orderId, request);
		await PublishMessageAsync(orderId, request);
		_logger.LogWarning(">> CreateOrderAsync: {ActivityId}", Activity.Current?.Id);
		return new OrderResult
		{
			OrderId = orderId,
			Status = "Created"
		};
	}

	private async Task<string> CallGrpcServiceAsync(string orderId, CreateOrderRequest request)
	{
		try
		{
			// In production, use HttpClientFactory and service discovery
			var grpcAddress = _configuration["GrpcApi:Address"]!;

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
			var myContext = MyContextHelper.GetBaggage()!;
			_logger.LogInformation(
				"-- WebApi -- Processing order {OrderId} for customer {CustomerName} with amount {Amount}. " +
				"Baggage -> TenantId: {TenantId}, UserId: {UserId}, TraceId {TraceId}",
				grpcRequest.OrderId,
				grpcRequest.CustomerName,
				grpcRequest.Amount,
				myContext.TenantId,
				myContext.UserId,
				Activity.Current!.TraceId.ToString()
			);

			return reply.Message;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error calling gRPC service");
			return $"gRPC call failed: {ex.Message}";
		}
	}

	private async Task PublishMessageAsync(string orderId, CreateOrderRequest request)
	{
		try
		{
			await this.PublishRabbitMqMessageAsync(orderId, request);
			_logger.LogInformation("Published message for order {OrderId}", orderId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error publishing message to RabbitMQ");
		}

		try
		{
			await this.PublishServiceBusMessageAsync(orderId, request);
			_logger.LogInformation("Published message for order {OrderId}", orderId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error publishing message to ServiceBus");
		}
	}

	private async Task PublishRabbitMqMessageAsync(string orderId, CreateOrderRequest request)
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
		};
		var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
		var props = new BasicProperties { Headers = new Dictionary<string, object?>() };
		ActivityHelper.Inject(props.Headers, Activity.Current);

		await channel.BasicPublishAsync(
			exchange: string.Empty,
			routingKey: "orders",
			true,
			basicProperties: props,
			body: body
		);

		_logger.LogInformation("Published message for order {OrderId}", orderId);
	}

	private async Task PublishServiceBusMessageAsync(string orderId, CreateOrderRequest request)
	{
		var client = new ServiceBusClient(_configuration["ConnectionStrings:ServiceBus"]);
		var sender = client.CreateSender("orders");

		var payload = new OrderCreatedMessage
		{
			OrderId = orderId,
			CustomerName = request.CustomerName,
			Amount = request.Amount,
			CreatedAt = DateTime.UtcNow,
		};
		var message = new ServiceBusMessage(JsonSerializer.Serialize(payload));
		ActivityHelper.Inject(message.ApplicationProperties, Activity.Current);
		await sender.SendMessageAsync(message);
	}
}

public class CreateOrderRequest
{
	public required string CustomerName { get; init; }
	public decimal Amount { get; init; }
}

public class OrderResult
{
	public required string OrderId { get; init; }
	public required string Status { get; init; }
	public string? GrpcResponse { get; init; }
}
