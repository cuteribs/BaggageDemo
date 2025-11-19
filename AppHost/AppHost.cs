var builder = DistributedApplication.CreateBuilder(args);

var configuration = builder.Configuration;

builder.AddProject<Projects.BaggageDemo_GrpcApi>("GrpcApi");

//builder.AddProject<Projects.BaggageDemo_MessageHandler>("MessageHandler");

builder.AddProject<Projects.BaggageDemo_WebApi>("WebApi")
	.WithEnvironment("ServiceBus:ConnectionString", configuration["ServiceBus:ConnectionString"]);

builder.AddProject<Projects.BaggageDemo_FunctionApp>("FunctionApp")
	.WithEnvironment("RabbitMQConnection", configuration["RabbitMQ:Host"])
	.WithEnvironment("ServiceBusConnection:connectionString", configuration["ServiceBus:ConnectionString"]);

builder.Build().Run();
