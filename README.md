# MongoDB Replica Set Support for Aspire

[![nuget](https://img.shields.io/nuget/v/Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet.svg?label=nuget)](https://www.nuget.org/packages/Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet)

This project provides support of MongoDB replica sets in Aspire.

It uses MongoDB horizons feature to correctly handle replica set configuration and make it available from both Aspire internal container network and host machine.
The replica set correctly handles container restarts and Aspire process restarts.

This package supports both single instance replica sets and multi-instance replica sets, allowing you to choose the configuration that best suits your development and testing needs.

Both original MongoDB Server and Percona Server for MongoDB are supported, giving you flexibility in your choice of MongoDB distribution.

> [!NOTE]
> MongoDB replica set horizons require TLS, thus this package will enforce TLS configuration for MongoDB containers. 
> See the samples below for more details.

# Getting Started

## Installation

Install the package from NuGet:

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

> [!TIP]
> As replica set horizons require TLS, the MongoDB server will be configured to use ASP.NET core development certificate by default.
> As an alternative, you can provide your own TLS certificate and configure the server to use it.
> Please, see the multiple instance replica set sample below for more details.

## Creating a Multiple Instance Replica Set

For more complex scenarios, you may want to create a replica set with multiple instances.

It's a bit more complex to set up, because ASP.NET development certificate cannot be used for that purpose, 
so you will need to provide your own TLS certificate and configure MongoDB servers to use it.
This package provides a helper method to create a self-signed TLS certificate for MongoDB servers,
so you can use it for development and testing purposes.

Additionally, you will need to configure the same administrator password for all MongoDB servers.

Thus, the process of creating a multiple instance replica set looks like this:
```csharp

#pragma warning disable ASPIRECERTIFICATES001

var passwordParameter = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, "mongo-password", special: false);
var passwordParameterBuilder = builder.CreateResourceBuilder(passwordParameter);

var mongoServer1 = builder.AddMongoDB("TestMongo1", 27017, password: passwordParameterBuilder);
var mongoServer2 = builder.AddMongoDB("TestMongo2", 27018, password: passwordParameterBuilder);
var mongoServer3 = builder.AddMongoDB("TestMongo3", 27019, password: passwordParameterBuilder);

mongoServer1.AddDatabase("TestMongoDB1", "TestDB");
mongoServer2.AddDatabase("TestMongoDB2", "TestDB");
mongoServer3.AddDatabase("TestMongoDB3", "TestDB");

var mongoCertificate = MongoDBCertificateUtils.CreateSelfSignedCertificate();

var mongoCertificateAuthority = builder.AddCertificateAuthorityCollection("TestMongoCA")
    .WithCertificate(mongoCertificate);

var mongoRs = builder.AddMongoDBReplicaSet("TestMongoRS")
    .WithMember(mongoServer1)
    .WithMember(mongoServer2)
    .WithMember(mongoServer3)
    .WithHttpsCertificate(mongoCertificate)
    .WithCertificateAuthorityCollection(mongoCertificateAuthority)
    .WithDbGate();
```

You will also need to call `WithCertificateAuthorityCollection(mongoCertificateAuthority)` on the resources accessing the replica set, 
to make the certificate authority collection available in them and allow them to trust the MongoDB servers.
However, note the `ProjectResource` does not really support `WithCertificateAuthorityCollection` (it's a no-op),
so for such resources you can do one of the following:
+ Add the certificate to the trusted root certificate authorities store of the host machine for your user.
+ Set `MongoClientSettings.AllowInsecureTls = true` in the client code.
+ Set `MongoClientSettings.SslSettings.ServerCertificateValidationCallback` to perform a custom certificate validation in the client code. See the `samples` folder for an example of how you can do that.

> [!NOTE]
> In case you use persistent MongoDB server instances, you should ensure that the same TLS certificate is used for them across restarts.
> In order to achieve that, you can store the self-signed certificate in a `ParameterResource` and use it to configure the servers.

## Using Percona Server for MongoDB

This package also supports Percona Server for MongoDB, which is a popular alternative to the original MongoDB Server.
In order to use Percona Server for MongoDB, you can use the `AddPerconaServerForMongoDB` extension method instead of `AddMongoDB`:

```csharp
var mongoServer = builder.AddPerconaServerForMongoDB("TestMongo", 27017);
```

## Using DbGate

This package also provides support for DbGate, a popular database management tool.
Instead of adding DbGate to the replica set members, add it directly to the replica set resource using `WithDbGate()` extension method:

```csharp
var mongoRS = builder.AddMongoDBReplicaSet("TestMongoRS")
    .WithMember(mongoServer)
    .WithDbGate();
```