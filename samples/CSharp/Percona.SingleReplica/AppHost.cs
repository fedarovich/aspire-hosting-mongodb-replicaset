using Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;
using Projects;
using Shared;

var builder = DistributedApplication.CreateBuilder(args);

var mongoServer = builder.AddPerconaServerForMongoDB("TestMongo", 27017);

mongoServer.AddDatabase("TestMongoDB", "TestDB");

var mongoRs = builder.AddMongoDBReplicaSet("TestMongoRS")
    .WithMember(mongoServer)
    .WithDbGate();

// Add sample services that use the replica set.

var dotNetLocal = builder.AddProject<WebApi>("DotNet-Local")
    .WithReference(mongoRs);

var dotNetContainer = builder.AddDockerfileForProject<WebApi>("DotNet-Container")
    .WithReference(mongoRs);

builder.Build().Run();