#pragma warning disable ASPIRECERTIFICATES001

using Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;
using Projects;
using Shared;

var builder = DistributedApplication.CreateBuilder(args);

var mongoServer = builder.AddMongoDB("TestMongo", 27017);

mongoServer.AddDatabase("TestMongoDB", "TestDB");

var mongoRs = builder.AddMongoDBReplicaSet("TestMongoRS")
    .WithMember(mongoServer)
    .WithDbGate();

// Add sample services that use the replica set.

var dotNetLocal = builder.AddProject<WebApi>("DotNet-Local")
    .WithReference(mongoRs);

var dotNetContainer = builder.AddDockerfileForProject<WebApi>("DotNet-Container")
    .WithReference(mongoRs);

var nodeLocal = builder.AddJavaScriptApp("NodeJS-Local", "../../TypeScript/WebApi")
    .WithHttpEndpoint(targetPort: 3000)
    .WithReference(mongoRs);

var nodeContainer = builder.AddDockerfile("NodeJS-Container", "../../TypeScript/WebApi")
    .WithHttpEndpoint(targetPort: 3000)
    .WithReference(mongoRs);

var pythonContainer = builder.AddDockerfile("Python-Container", "../../Python/WebApi")
    .WithHttpEndpoint(targetPort: 8000)
    .WithReference(mongoRs);

builder.Build().Run();