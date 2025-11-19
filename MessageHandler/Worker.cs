using Azure.Core;
using Azure.Messaging.ServiceBus;
using BaggageDemo.Common;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BaggageDemo.MessageHandler;

public class OrderMessageWorker : BackgroundService
{
	private static readonly ActivitySource TraceSource = new("BaggageDemo.MessageHandler");

	private readonly ILogger<OrderMessageWorker> _logger;
	private readonly IConfiguration _configuration;
	private IConnection? _rmqConnection;
	private IChannel? _rmqChannel;

	public OrderMessageWorker(ILogger<OrderMessageWorker> logger, IConfiguration configuration)
	{
		_logger = logger;
		_configuration = configuration;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			await this.ConsumeRabbitMQAsync(stoppingToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error occurred while consuming RabbitMQ messages");
		}

		try
		{
			await this.ConsumeServiceBusAsync(stoppingToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error occurred while consuming Service Bus messages");
		}

		_logger.LogInformation("Message consumer started");

		// Keep the worker running
		while (!stoppingToken.IsCancellationRequested)
		{
			await Task.Delay(1000, stoppingToken);
		}
	}

	private async Task ConsumeRabbitMQAsync(CancellationToken stoppingToken)
	{
		var rabbitHost = _configuration["RabbitMQ:Host"] ?? "localhost";
		var factory = new ConnectionFactory { HostName = rabbitHost };
		_rmqConnection ??= await factory.CreateConnectionAsync(stoppingToken);
		_rmqChannel ??= await _rmqConnection.CreateChannelAsync(cancellationToken: stoppingToken);

		try
		{
			await _rmqChannel.QueueDeclareAsync(
					queue: "orders",
					durable: true,
					exclusive: false,
					autoDelete: false,
					arguments: null,
					cancellationToken: stoppingToken);

			_logger.LogInformation("Message handler connected to RabbitMQ and listening on 'orders' queue");

			var consumer = new AsyncEventingBasicConsumer(_rmqChannel);
			consumer.ReceivedAsync += async (model, ea) =>
			{
				var propagationContext = Propagators.DefaultTextMapPropagator.Extract(
					default,
					ea.BasicProperties.Headers,
					(props, key) =>
					{
						if (props!.TryGetValue(key, out var value) && value is byte[] bytes)
						{
							return [Encoding.UTF8.GetString(bytes)];
						}

						return null;
					}
				);
				Baggage.Current = propagationContext.Baggage;
				using var activity = TraceSource.StartActivity(
					nameof(ConsumeRabbitMQAsync),
					ActivityKind.Consumer,
					propagationContext.ActivityContext
				);

				try
				{
					_logger.LogInformation("Received RabbitMQ message");
					var body = Encoding.UTF8.GetString(ea.Body.ToArray());
					var order = JsonSerializer.Deserialize<OrderCreatedMessage>(body)!;

					var myContext = MyContextHelper.GetBaggage()!;
					_logger.LogWarning(
						"-- MessageHandler -- Processing order {OrderId} for customer {CustomerName} with amount {Amount}. " +
						"Baggage -> TenantId: {TenantId}, UserId: {UserId}, TraceId {TraceId}",
						order.OrderId,
						order.CustomerName,
						order.Amount,
						myContext.TenantId,
						myContext.UserId,
						Activity.Current!.TraceId.ToString()
					);

					await _rmqChannel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error processing message");
					await _rmqChannel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
				}
			};

			await _rmqChannel.BasicConsumeAsync(
				queue: "orders",
				autoAck: false,
				consumer: consumer,
				cancellationToken: stoppingToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to connect to RabbitMQ");
		}
	}

	private async Task ConsumeServiceBusAsync(CancellationToken stoppingToken)
	{
		var client = new ServiceBusClient(_configuration["ServiceBus:ConnectionString"]);
		var processor = client.CreateProcessor("orders", new ServiceBusProcessorOptions());

		processor.ProcessMessageAsync += async args =>
		{
			var propagationContext = Propagators.DefaultTextMapPropagator.Extract(
				default,
				args.Message.ApplicationProperties,
				(props, key) =>
				{
					if (props!.TryGetValue(key, out var value) && value is string str)
					{
						return [str];
					}

					return null;
				}
			);
			Baggage.Current = propagationContext.Baggage;
			using var activity = TraceSource.StartActivity(
				nameof(ConsumeRabbitMQAsync),
				ActivityKind.Consumer,
				propagationContext.ActivityContext
			);

			_logger.LogInformation("Received ServiceBus message");

			var body = args.Message.Body.ToString();
			var order = JsonSerializer.Deserialize<OrderCreatedMessage>(body)!;

			var myContext = MyContextHelper.GetBaggage()!;
			_logger.LogWarning(
				"-- MessageHandler -- Processing order {OrderId} for customer {CustomerName} with amount {Amount}. " +
				"Baggage -> TenantId: {TenantId}, UserId: {UserId}, TraceId {TraceId}",
				order.OrderId,
				order.CustomerName,
				order.Amount,
				myContext.TenantId,
				myContext.UserId,
				Activity.Current!.TraceId.ToString()
			);

			// 完成消息
			await args.CompleteMessageAsync(args.Message);
		};

		processor.ProcessErrorAsync += args =>
		{
			Console.WriteLine($"Error: {args.Exception.Message}");
			return Task.CompletedTask;
		};

		// 开始处理消息
		await processor.StartProcessingAsync();
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await _rmqChannel?.CloseAsync();
		await _rmqConnection?.CloseAsync();
		await base.StopAsync(cancellationToken);
	}
}
