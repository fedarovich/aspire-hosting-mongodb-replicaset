using Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var mongoServer = builder.AddPerconaServerForMongoDB("TestMongo", 27017);

mongoServer.AddDatabase("TestMongoDB", "TestDB");

var mongoRs = builder.AddMongoDBReplicaSet("TestMongoRS")
    .WithMember(mongoServer)
    .WithDbGate();

var webApiLocal = builder.AddProject<WebApi>("WebApi-Local")
    .WithReference(mongoRs);

builder.Build().Run();