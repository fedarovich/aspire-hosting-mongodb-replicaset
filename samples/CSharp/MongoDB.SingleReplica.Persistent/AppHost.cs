using Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

var builder = DistributedApplication.CreateBuilder(args);

var mongoServer = builder.AddMongoDB("TestMongo", 27017)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

mongoServer.AddDatabase("TestMongoDB", "TestDB");

var mongoRs = builder.AddMongoDBReplicaSet("TestMongoRS")
    .WithMember(mongoServer)
    .WithDbGate();

builder.Build().Run();