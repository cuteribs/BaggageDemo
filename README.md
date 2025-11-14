# OpenTelemetry Baggage Demo with .NET 8 Aspire

This solution demonstrates how to use **OpenTelemetry Baggage** to transport custom context across different communication patterns in a distributed .NET 8 application orchestrated by Aspire.

## Architecture Overview

The solution consists of the following services:

### 1. **BaggageDemo.WebApi** (HTTP API)
- Entry point for creating orders
- **Sets baggage** with tenant-id, correlation-id, and user-id
- Calls the gRPC service (baggage auto-propagates via OTel instrumentation)
- Publishes messages to RabbitMQ (baggage included in message payload)
- Endpoint: `POST /api/orders`

### 2. **BaggageDemo.GrpcApi** (gRPC Service)
- Receives order processing requests
- **Reads baggage** automatically propagated through gRPC metadata
- Demonstrates cross-service baggage propagation
- Service: `BaggageProcessor.ProcessOrder`

### 3. **BaggageDemo.MessageHandler** (Worker Service)
- Consumes messages from RabbitMQ queue
- **Extracts baggage** from message payload
- Sets baggage in Activity for downstream operations
- Queue: `orders`

### 4. **BaggageDemo.AppHost** (Aspire Orchestrator)
- Orchestrates all services and dependencies
- Manages RabbitMQ container
- Configures service discovery and health checks

## Baggage Propagation Patterns

### Pattern 1: HTTP/gRPC → Automatic Propagation
OpenTelemetry instrumentation automatically propagates baggage through HTTP headers and gRPC metadata:
```csharp
// In WebApi - Set baggage
activity.SetBaggage("tenant-id", request.TenantId);
activity.SetBaggage("correlation-id", correlationId);
activity.SetBaggage("user-id", request.UserId);

// In GrpcApi - Read baggage (automatically available)
var tenantId = Activity.Current?.GetBaggageItem("tenant-id");
var correlationId = Activity.Current?.GetBaggageItem("correlation-id");
```

### Pattern 2: Messaging → Manual Propagation
For message queues, baggage is included in the message payload:
```csharp
// In WebApi - Include baggage in message
var message = new OrderCreatedMessage
{
    TenantId = tenantId,
    CorrelationId = correlationId,
    UserId = userId
};

// In MessageHandler - Restore baggage from message
activity.SetBaggage("tenant-id", message.TenantId);
activity.SetBaggage("correlation-id", message.CorrelationId);
```

## Key Configuration

### ServiceDefaults (OpenTelemetry Setup)
Located in `BaggageDemo.ServiceDefaults/Extensions.cs`:
- Configures ASP.NET Core instrumentation
- Enables gRPC client instrumentation for baggage propagation
- Enables HTTP client instrumentation
- Configures OTLP exporters for observability

```csharp
.WithTracing(tracing =>
{
    tracing.AddAspNetCoreInstrumentation(options => { ... })
           .AddGrpcClientInstrumentation(options => { ... })
           .AddHttpClientInstrumentation(options => { ... });
});
```

## Running the Solution

### Prerequisites
- .NET 8 SDK or later
- Docker (for RabbitMQ)
- Aspire Workload: `dotnet workload install aspire`

### Steps

1. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

2. **Run with Aspire AppHost**:
   ```bash
   dotnet run --project BaggageDemo.AppHost
   ```

3. **Access the Aspire Dashboard**:
   - Open the URL displayed in the console (typically `https://localhost:17XXX`)
   - View all services, their health, logs, and traces

4. **Test the baggage flow**:
   ```bash
   # Create an order via WebApi
   curl -X POST https://localhost:<webapi-port>/api/orders \
     -H "Content-Type: application/json" \
     -d '{
       "customerName": "John Doe",
       "amount": 150.00,
       "tenantId": "tenant-123",
       "userId": "user-456"
     }'
   ```

5. **Observe baggage propagation**:
   - Check WebApi logs: Baggage set with tenant-id, correlation-id, user-id
   - Check GrpcApi logs: Baggage received and logged
   - Check MessageHandler logs: Baggage extracted from message and logged

## Projects

| Project | Type | Purpose |
|---------|------|---------|
| BaggageDemo.AppHost | Aspire Host | Orchestrates all services and dependencies |
| BaggageDemo.WebApi | ASP.NET Core Web API | Entry point, sets baggage, calls downstream services |
| BaggageDemo.GrpcApi | gRPC Service | Receives baggage via gRPC metadata |
| BaggageDemo.MessageHandler | Worker Service | Consumes messages with baggage |
| BaggageDemo.ServiceDefaults | Class Library | Shared OpenTelemetry configuration |
| BaggageDemo.Contracts | Class Library | Shared message contracts |

## Observability

The solution is fully instrumented with OpenTelemetry:

- **Traces**: Distributed tracing across all services
- **Metrics**: Runtime, HTTP, and ASP.NET Core metrics
- **Logs**: Structured logging with baggage context
- **Baggage**: Custom context propagation (tenant-id, correlation-id, user-id)

View telemetry in:
- Aspire Dashboard (built-in)
- Any OTLP-compatible backend (Jaeger, Zipkin, Application Insights, etc.)

## Use Cases for Baggage

1. **Multi-tenancy**: Propagate tenant ID across all services
2. **Correlation**: Track requests across distributed system
3. **User Context**: Maintain user identity throughout request flow
4. **Feature Flags**: Propagate feature toggle state
5. **A/B Testing**: Carry experiment identifiers
6. **Request Metadata**: Transport custom business context

## Notes

- Baggage is automatically propagated for HTTP and gRPC calls via OTel instrumentation
- For messaging, baggage must be manually included in message payload
- Baggage adds overhead to HTTP headers - use sparingly (recommended: 3-5 key-value pairs)
- All services reference `ServiceDefaults` for consistent OpenTelemetry configuration
- RabbitMQ is managed by Aspire and runs in a container

## Learn More

- [OpenTelemetry Baggage Specification](https://opentelemetry.io/docs/concepts/signals/baggage/)
- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet)
