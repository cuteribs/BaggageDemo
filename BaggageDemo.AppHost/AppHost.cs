var builder = DistributedApplication.CreateBuilder(args);

// Add RabbitMQ for message-based communication
var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin();

// Add gRPC API service
var grpcApi = builder.AddProject<Projects.BaggageDemo_GrpcApi>("grpcapi")
    .WithHttpHealthCheck("/health");

// Add WebAPI service with references to gRPC and RabbitMQ
var webApi = builder.AddProject<Projects.BaggageDemo_WebApi>("webapi")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/api/health")
    .WithReference(grpcApi)
    .WithReference(rabbitmq)
    .WithEnvironment("GrpcApi:Address", grpcApi.GetEndpoint("https"))
    .WaitFor(grpcApi)
    .WaitFor(rabbitmq);

// Add Message Handler service
var messageHandler = builder.AddProject<Projects.BaggageDemo_MessageHandler>("messagehandler")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq);

// Keep original services for comparison
var apiService = builder.AddProject<Projects.BaggageDemo_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.BaggageDemo_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
