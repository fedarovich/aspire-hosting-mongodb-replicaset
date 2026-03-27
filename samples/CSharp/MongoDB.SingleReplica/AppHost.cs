using Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

var builder = DistributedApplication.CreateBuilder(args);

var mongoServer = builder.AddMongoDB("TestMongo", 27017);

mongoServer.AddDatabase("TestMongoDB", "TestDB");

var mongoRs = builder.AddMongoDBReplicaSet("TestMongoRS")
    .WithMember(mongoServer);

builder.Build().Run();