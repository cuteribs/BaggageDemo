# Example Output & Logs

This document shows what you should see when running the Baggage Demo.

## Starting the Application

```bash
$ dotnet run --project BaggageDemo.AppHost

Building...
info: Aspire.Hosting.DistributedApplication[0]
      Aspire version: 13.0.0
info: Aspire.Hosting.DistributedApplication[0]
      Distributed application starting.
info: Aspire.Hosting.DistributedApplication[0]
      Application host directory is: C:\git\dnv\BaggageDemo\BaggageDemo.AppHost
info: Aspire.Hosting.DistributedApplication[0]
      Now listening on: https://localhost:17001
info: Aspire.Hosting.DistributedApplication[0]
      Login to the dashboard at https://localhost:17001/login?t=xxxxx
```

## Service Startup Logs

### WebAPI Service
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7123
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
```

### GrpcApi Service
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7214
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### MessageHandler Service
```
info: BaggageDemo.MessageHandler.OrderMessageWorker[0]
      Message handler connected to RabbitMQ and listening on 'orders' queue
info: BaggageDemo.MessageHandler.OrderMessageWorker[0]
      Message consumer started
```

## Creating an Order

### Request
```bash
curl -X POST https://localhost:7123/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerName": "Alice Johnson",
    "amount": 299.99,
    "tenantId": "tenant-acme-corp",
    "userId": "user-alice-123"
  }' -k
```

### Response
```json
{
  "orderId": "a7f3b2c1-4d5e-6f78-9012-3456789abcde",
  "status": "Created",
  "grpcResponse": "Order a7f3b2c1-4d5e-6f78-9012-3456789abcde processed successfully",
  "tenantId": "tenant-acme-corp",
  "correlationId": "b8c4d3e2-5f6a-7890-1234-56789abcdef0",
  "userId": "user-alice-123"
}
```

## Log Output - Baggage Flow

### 1. WebAPI Logs (Setting Baggage)

```
info: BaggageDemo.WebApi.Services.OrderService[0]
      Creating order for customer Alice Johnson with amount 299.99. 
      Baggage -> TenantId: tenant-acme-corp, 
      CorrelationId: b8c4d3e2-5f6a-7890-1234-56789abcdef0, 
      UserId: user-alice-123
```

**Notice**: The WebAPI sets the baggage in the current Activity context.

---

### 2. GrpcApi Logs (Receiving Baggage via gRPC)

```
info: BaggageDemo.GrpcApi.Services.BaggageProcessorService[0]
      Processing order a7f3b2c1-4d5e-6f78-9012-3456789abcde 
      for customer Alice Johnson with amount 299.99. 
      Baggage -> TenantId: tenant-acme-corp, 
      CorrelationId: b8c4d3e2-5f6a-7890-1234-56789abcdef0, 
      UserId: user-alice-123
```

**Notice**: The **exact same** baggage values are automatically available in the gRPC service! OpenTelemetry propagated them via gRPC metadata headers.

---

### 3. WebAPI Logs (After gRPC Call)

```
info: BaggageDemo.WebApi.Services.OrderService[0]
      gRPC response: Order a7f3b2c1-4d5e-6f78-9012-3456789abcdef0 processed successfully, 
      TenantId: tenant-acme-corp, 
      CorrelationId: b8c4d3e2-5f6a-7890-1234-56789abcdef0

info: BaggageDemo.WebApi.Services.OrderService[0]
      Published message for order a7f3b2c1-4d5e-6f78-9012-3456789abcdef0 
      with baggage TenantId: tenant-acme-corp, 
      CorrelationId: b8c4d3e2-5f6a-7890-1234-56789abcdef0
```

---

### 4. MessageHandler Logs (Receiving Baggage via RabbitMQ)

```
info: BaggageDemo.MessageHandler.OrderMessageWorker[0]
      Processing order message: 
      OrderId=a7f3b2c1-4d5e-6f78-9012-3456789abcdef0, 
      Customer=Alice Johnson, Amount=299.99. 
      Baggage -> TenantId: tenant-acme-corp, 
      CorrelationId: b8c4d3e2-5f6a-7890-1234-56789abcdef0, 
      UserId: user-alice-123

info: BaggageDemo.MessageHandler.OrderMessageWorker[0]
      Successfully processed order a7f3b2c1-4d5e-6f78-9012-3456789abcdef0 
      with correlation b8c4d3e2-5f6a-7890-1234-56789abcdef0
```

**Notice**: The baggage is present in the message consumer as well! It was included in the RabbitMQ message payload.

---

## Aspire Dashboard View

### Resources Tab
```
NAME            TYPE        STATE     ENDPOINTS
messaging       Container   Running   tcp://localhost:5672, http://localhost:15672
webapi          Project     Running   https://localhost:7123
grpcapi         Project     Running   https://localhost:7214
messagehandler  Project     Running   (worker, no endpoint)
apiservice      Project     Running   https://localhost:7xxx
webfrontend     Project     Running   https://localhost:7xxx
```

### Traces Tab (Distributed Trace Example)

```
Trace: b8c4d3e2-5f6a-7890-1234-56789abcdef0
Duration: 145ms

┌─ POST /api/orders (webapi) - 145ms
│  Baggage: tenant-id=tenant-acme-corp, correlation-id=b8c4d3e2-..., user-id=user-alice-123
│
├──┌─ gRPC baggage.BaggageProcessor/ProcessOrder (grpcapi) - 12ms
│  │  Baggage: tenant-id=tenant-acme-corp, correlation-id=b8c4d3e2-..., user-id=user-alice-123
│  └─
│
├──┌─ RabbitMQ Publish (webapi) - 8ms
│  └─
└─
```

**Notice**: The trace shows the baggage propagating from WebAPI to GrpcApi automatically!

### Logs Tab (Filtered by Correlation ID)

When filtering logs by the correlation-id, you'll see entries from all three services showing the complete flow of a single order.

## Key Observations

### ✅ Automatic Propagation (HTTP/gRPC)
- Baggage set in WebAPI is **automatically** available in GrpcApi
- No manual header manipulation required
- OpenTelemetry instrumentation handles everything

### ✅ Manual Propagation (Messaging)
- Baggage included in message payload
- MessageHandler extracts and sets it in Activity
- Maintains context across async boundaries

### ✅ Consistent Correlation
- The same `correlation-id` appears in logs across all services
- Enables end-to-end request tracking
- Simplifies debugging and observability

### ✅ Multi-Tenant Context
- `tenant-id` propagates to all services
- Each service can make tenant-specific decisions
- No need to pass tenant info in every method call

## Testing Different Scenarios

### Scenario 1: Multiple Tenants
```bash
# Order 1 - Tenant A
curl -X POST ... -d '{"tenantId": "tenant-A", ...}'

# Order 2 - Tenant B  
curl -X POST ... -d '{"tenantId": "tenant-B", ...}'
```

**Result**: Each tenant's context flows through the entire system independently.

### Scenario 2: Custom Correlation ID
```bash
curl -X POST ... -d '{"correlationId": "my-custom-id-123", ...}'
```

**Result**: Your custom correlation ID is used instead of auto-generated one.

### Scenario 3: Trace in Dashboard
1. Send request
2. Note the correlation ID from response
3. Search traces by correlation ID in Aspire Dashboard
4. See complete distributed trace with baggage

## Performance Impact

Baggage adds minimal overhead:
- ~200-500 bytes per request (for 3-5 key-value pairs)
- Negligible latency impact (<1ms)
- Worth it for observability benefits

**Best Practice**: Keep baggage small (3-5 items, short values).
