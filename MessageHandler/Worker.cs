using Azure.Messaging.ServiceBus;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;

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
					_logger.LogWarning("Received RabbitMQ message");
					var body = ea.Body.ToArray();
					var messageJson = Encoding.UTF8.GetString(body);
					_logger.LogWarning($"[x] Received: {messageJson}");

					foreach (var item in Baggage.Current)
					{
						_logger.LogWarning($"Baggage: {item.Key} = {item.Value}");
					}

					await _rmqChannel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error processing message");
					await _rmqChannel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
				}
				_logger.LogError("MessageHandler {TraceId}", activity?.TraceId.ToString());
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

			_logger.LogWarning("Received ServiceBus message");

			// 打印提取的 Baggage 信息
			foreach (var item in Baggage.Current)
			{
				_logger.LogWarning($"Baggage: {item.Key} = {item.Value}");
			}

			// 处理接收到的消息
			var body = args.Message.Body.ToString();
			_logger.LogWarning($"[x] Received: {body}");

			// 完成消息
			await args.CompleteMessageAsync(args.Message);
			_logger.LogError("MessageHandler {TraceId}", activity?.TraceId.ToString());
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
