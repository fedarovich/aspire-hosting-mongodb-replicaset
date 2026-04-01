# MongoDB Replica Set Support for Aspire

This project provides support of MongoDB replica sets in [Aspire](https://aspire.dev).

It uses MongoDB horizons feature to correctly handle replica set configuration and make it available from both Aspire internal container network and host machine.
The replica set correctly handles container restarts and Aspire process restarts.

This package supports both single instance replica sets and multi-instance replica sets, allowing you to choose the configuration that best suits your development and testing needs.

Both original MongoDB Server and Percona Server for MongoDB are supported, giving you flexibility in your choice of MongoDB distribution.

## Installation

Install the package from [![nuget](https://img.shields.io/nuget/v/Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet.svg?label=NuGet)](https://www.nuget.org/packages/Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet):

```bash
dotnet add package Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet
```

## Creating a Single Instance Replica Set

For most development and testing scenarios, a single instance replica set is sufficient.
It provides the benefits of replica sets without the overhead of managing multiple instances.

Creating a single instance replica set is straightforward with this package:

```csharp

// Add MongoDB server with a fixed port
var mongoServer = builder.AddMongoDB("TestMongo", 27017);

// Add database to the server
mongoServer.AddDatabase("TestMongoDB", "TestDB");

// Add replica set with the server as a member
var mongoRS = builder.AddMongoDBReplicaSet("TestMongoRS")
    .WithMember(mongoServer);

// Reference the replica set from other resources
var webApiLocal = builder.AddProject<WebApi>("WebApi-Local")
    .WithReference(mongoRs);

```

The created `MongoDBReplicaSet` resource provides the connection string and can be used as an argument of `.WithReference()` extension method.

You can also do it with TypeScript AppHost:
```typescript

// Add MongoDB server with a fixed port
const mongoServer = await builder.addMongoDB('TestMongo', { port: 27017 });

// Add database to the server
await mongoServer.addDatabase('TestMongoDB');

// Add replica set with the server as a member
const mongoRS = await builder.addMongoDBReplicaSet('TestMongoRS')
    .withMember(mongoServer);

// Reference the replica set from other resources
const nodeLocal = await builder.addJavaScriptApp('NodeJS-Local', '../WebApi')
    .withHttpEndpoint({ targetPort: 3000 })
    .withReference(mongoRS);

```

## Samples
See the project repository for more samples and documentation: [Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet](https://github.com/fedarovich/aspire-hosting-mongodb-replicaset)