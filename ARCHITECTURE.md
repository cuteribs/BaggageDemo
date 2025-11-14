# Architecture Diagrams

## Service Communication Flow

```mermaid
graph TD
    Client[HTTP Client] -->|POST /api/orders| WebAPI[WebAPI Service]
    
    WebAPI -->|Set Baggage| Activity[Activity/Context]
    Activity -->|tenant-id<br/>correlation-id<br/>user-id| WebAPI
    
    WebAPI -->|gRPC Call<br/>Auto-propagated baggage| GrpcApi[gRPC Service]
    WebAPI -->|Publish Message<br/>Baggage in payload| RabbitMQ[(RabbitMQ)]
    
    RabbitMQ -->|Consume Message| MessageHandler[Message Handler]
    
    GrpcApi -->|Read Baggage| ActivityG[Activity.Current]
    MessageHandler -->|Restore Baggage| ActivityM[Activity.Current]
    
    style Activity fill:#e1f5ff
    style ActivityG fill:#e1f5ff
    style ActivityM fill:#e1f5ff
```

## Baggage Propagation Mechanisms

```mermaid
sequenceDiagram
    participant Client
    participant WebAPI
    participant Activity
    participant GrpcApi
    participant RabbitMQ
    participant MessageHandler

    Client->>WebAPI: POST /api/orders
    WebAPI->>Activity: SetBaggage("tenant-id", value)
    WebAPI->>Activity: SetBaggage("correlation-id", value)
    WebAPI->>Activity: SetBaggage("user-id", value)
    
    Note over WebAPI,GrpcApi: Automatic Propagation via OTel
    WebAPI->>GrpcApi: gRPC ProcessOrder
    Note right of GrpcApi: Baggage in gRPC metadata
    GrpcApi->>Activity: GetBaggageItem("tenant-id")
    GrpcApi-->>WebAPI: Order processed
    
    Note over WebAPI,MessageHandler: Manual Propagation via Message
    WebAPI->>RabbitMQ: Publish OrderCreatedMessage<br/>(includes baggage fields)
    RabbitMQ->>MessageHandler: Consume message
    MessageHandler->>Activity: SetBaggage from message payload
    
    WebAPI-->>Client: Order response with baggage
```

## Component Structure

```
BaggageDemo Solution
│
├── BaggageDemo.AppHost (Aspire Orchestrator)
│   ├── Manages service lifecycle
│   ├── Configures RabbitMQ container
│   └── Sets up service discovery
│
├── BaggageDemo.WebApi (HTTP Entry Point)
│   ├── Creates orders
│   ├── SETS baggage in Activity
│   ├── Calls GrpcApi (auto-propagation)
│   └── Publishes to RabbitMQ (manual)
│
├── BaggageDemo.GrpcApi (gRPC Service)
│   ├── Processes orders
│   └── READS baggage (auto-received)
│
├── BaggageDemo.MessageHandler (Worker)
│   ├── Consumes RabbitMQ messages
│   └── EXTRACTS baggage from payload
│
├── BaggageDemo.ServiceDefaults (Shared Config)
│   ├── OpenTelemetry configuration
│   ├── Baggage propagation setup
│   └── Instrumentation configuration
│
└── BaggageDemo.Contracts (Shared Models)
    └── Message definitions
```

## Baggage Data Flow

```
┌─────────────────────────────────────────────────────┐
│ Baggage Items (Key-Value Pairs)                    │
├─────────────────────────────────────────────────────┤
│ • tenant-id: "tenant-acme-corp"                     │
│ • correlation-id: "a1b2c3d4-..."                    │
│ • user-id: "user-alice-123"                         │
└─────────────────────────────────────────────────────┘
                      ↓
        ┌─────────────────────────┐
        │  WebAPI sets in Activity │
        └─────────────────────────┘
                      ↓
        ┌─────────────┴─────────────┐
        ↓                           ↓
┌───────────────────┐    ┌──────────────────────┐
│ HTTP/gRPC Headers │    │ RabbitMQ Message     │
│ (automatic)       │    │ Payload (manual)     │
└────────┬──────────┘    └──────────┬───────────┘
         ↓                          ↓
┌─────────────────┐      ┌──────────────────────┐
│ GrpcApi reads   │      │ MessageHandler       │
│ from Activity   │      │ extracts & sets      │
└─────────────────┘      └──────────────────────┘
```

## Technology Stack

- **.NET 8**: Application framework
- **Aspire**: Orchestration and local development
- **OpenTelemetry**: Observability and baggage propagation
- **gRPC**: Inter-service communication
- **RabbitMQ**: Asynchronous messaging
- **ASP.NET Core**: Web API framework
