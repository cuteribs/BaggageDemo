# Testing the OpenTelemetry Baggage Demo

## Prerequisites

Ensure you have:
- .NET 8 SDK or later installed
- Docker running (for RabbitMQ)
- Aspire workload: `dotnet workload install aspire`

## Running the Application

1. **Start the Aspire AppHost**:
   ```bash
   cd BaggageDemo.AppHost
   dotnet run
   ```

2. **Access the Aspire Dashboard**:
   - The console will display a URL (e.g., `https://localhost:17001`)
   - Open it in your browser to see all services, logs, and traces

3. **Note the WebAPI port**:
   - In the Aspire dashboard, find the `webapi` service
   - Note its HTTPS endpoint (e.g., `https://localhost:7123`)

## Testing Baggage Propagation

### Using curl:

```bash
# Replace <webapi-port> with the actual port from Aspire dashboard
curl -X POST https://localhost:<webapi-port>/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerName": "Alice Johnson",
    "amount": 299.99,
    "tenantId": "tenant-acme-corp",
    "userId": "user-alice-123"
  }' \
  -k
```

### Using the .http file:

1. Open `test-requests.http` in VS Code
2. Install the REST Client extension if not already installed
3. Add at the top of the file:
   ```
   @webapi = https://localhost:<actual-port>
   ```
4. Click "Send Request" above each request

## What to Observe

### 1. WebAPI Logs
Look for log entries showing:
```
Creating order for customer Alice Johnson with amount 299.99.
Baggage -> TenantId: tenant-acme-corp, CorrelationId: <guid>, UserId: user-alice-123
```

### 2. GrpcApi Logs
The gRPC service should log:
```
Processing order <order-id> for customer Alice Johnson with amount 299.99.
Baggage -> TenantId: tenant-acme-corp, CorrelationId: <same-guid>, UserId: user-alice-123
```

**Key Point**: The baggage values (tenant-id, correlation-id, user-id) are automatically propagated from WebAPI to GrpcApi via OpenTelemetry instrumentation!

### 3. MessageHandler Logs
The message handler should log:
```
Processing order message: OrderId=<order-id>, Customer=Alice Johnson, Amount=299.99.
Baggage -> TenantId: tenant-acme-corp, CorrelationId: <same-guid>, UserId: user-alice-123
```

**Key Point**: The baggage is included in the RabbitMQ message payload and restored by the handler.

### 4. Aspire Dashboard

In the dashboard, you can:
- View **Traces**: See the distributed trace across WebAPI → GrpcApi
- View **Logs**: Filter by service to see baggage propagation
- View **Metrics**: Monitor service health and performance
- View **Resources**: See RabbitMQ status

## Baggage Flow

```
┌─────────────┐
│   WebAPI    │  Sets Baggage:
│             │  - tenant-id
│             │  - correlation-id
│             │  - user-id
└──────┬──────┘
       │
       ├─────────────────────────────┐
       │                             │
       │ gRPC Call                   │ RabbitMQ Message
       │ (Auto-propagated)           │ (Manual in payload)
       │                             │
       ▼                             ▼
┌─────────────┐              ┌──────────────────┐
│  GrpcApi    │              │ MessageHandler   │
│             │              │                  │
│ Reads       │              │ Extracts from    │
│ Baggage     │              │ message payload  │
└─────────────┘              └──────────────────┘
```

## Expected Response

```json
{
  "orderId": "a1b2c3d4-...",
  "status": "Created",
  "grpcResponse": "Order a1b2c3d4-... processed successfully",
  "tenantId": "tenant-acme-corp",
  "correlationId": "auto-generated-or-provided",
  "userId": "user-alice-123"
}
```

## Common Issues

### Issue: RabbitMQ not connecting
- **Solution**: Ensure Docker is running. Aspire will automatically start a RabbitMQ container.

### Issue: gRPC connection refused
- **Solution**: Wait a few seconds for all services to start. The Aspire dashboard shows service status.

### Issue: Baggage not propagating
- **Solution**: 
  - Verify ServiceDefaults is referenced in all projects
  - Check that `builder.AddServiceDefaults()` is called in each Program.cs
  - Ensure OpenTelemetry.Instrumentation.GrpcNetClient package is installed

## Next Steps

Try these experiments:

1. **Multiple tenants**: Send orders with different `tenantId` values
2. **Track correlation**: Use the same `correlationId` across multiple requests
3. **Add more baggage**: Extend with custom fields (e.g., region, experiment-id)
4. **View in Jaeger**: Configure OTLP exporter to send to Jaeger/Zipkin
5. **Add more services**: Create additional services that participate in the baggage flow
