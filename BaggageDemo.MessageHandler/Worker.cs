using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BaggageDemo.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BaggageDemo.MessageHandler;

public class OrderMessageWorker : BackgroundService
{
    private readonly ILogger<OrderMessageWorker> _logger;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IChannel? _channel;

    public OrderMessageWorker(ILogger<OrderMessageWorker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var rabbitHost = _configuration["RabbitMQ:Host"] ?? "localhost";
        var factory = new ConnectionFactory { HostName = rabbitHost };

        try
        {
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.QueueDeclareAsync(
                queue: "orders",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Message handler connected to RabbitMQ and listening on 'orders' queue");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel == null)
        {
            _logger.LogWarning("Channel is not initialized, message handler will not process messages");
            return;
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);
                var message = JsonSerializer.Deserialize<OrderCreatedMessage>(messageJson);

                if (message != null)
                {
                    // Create an activity to track this message processing
                    using var activity = new Activity("ProcessOrderMessage").Start();

                    // Set baggage from the message (simulating baggage propagation)
                    if (!string.IsNullOrEmpty(message.TenantId))
                        activity.SetBaggage("tenant-id", message.TenantId);
                    if (!string.IsNullOrEmpty(message.CorrelationId))
                        activity.SetBaggage("correlation-id", message.CorrelationId);
                    if (!string.IsNullOrEmpty(message.UserId))
                        activity.SetBaggage("user-id", message.UserId);

                    _logger.LogInformation(
                        "Processing order message: OrderId={OrderId}, Customer={CustomerName}, Amount={Amount}. " +
                        "Baggage -> TenantId: {TenantId}, CorrelationId: {CorrelationId}, UserId: {UserId}",
                        message.OrderId, message.CustomerName, message.Amount,
                        message.TenantId, message.CorrelationId, message.UserId);

                    // Simulate processing
                    await Task.Delay(100, stoppingToken);

                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);

                    _logger.LogInformation(
                        "Successfully processed order {OrderId} with correlation {CorrelationId}",
                        message.OrderId, message.CorrelationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: "orders",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Message consumer started");

        // Keep the worker running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }

        if (_connection != null)
        {
            await _connection.CloseAsync(cancellationToken);
            await _connection.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
