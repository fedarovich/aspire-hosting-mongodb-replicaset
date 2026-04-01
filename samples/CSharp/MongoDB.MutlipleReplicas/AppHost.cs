using Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;
using Projects;
using Shared;

#pragma warning disable ASPIRECERTIFICATES001

var builder = DistributedApplication.CreateBuilder(args);

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

var webApiLocal = builder.AddProject<WebApi>("WebApi-Local")
    .WithReference(mongoRs)
    .WithCertificateTrustScope(CertificateTrustScope.Append)
    .WithCertificateTrustConfiguration(context =>
    {
        context.EnvironmentVariables[$"{mongoRs.Resource.Name.ToUpperInvariant()}_CERTIFICATE_THUMBPRINT"] = mongoCertificate.Thumbprint;
        return Task.CompletedTask;
    });

// Add sample services that use the replica set.

// When running this project locally, it's not possible to override the certificate trust on the Aspire side,
// however we can add our custom validation callback on the service side.
// In this example, we pass the expected thumbprint of the MongoDB server certificate through an environment variable,
// and WebApi project is configured to read this value and add a custom validation callback that checks the server certificate thumbprint against the expected value.
var dotNetLocal = builder.AddProject<WebApi>("DotNet-Local")
    .WithReference(mongoRs)
    .WithCertificateTrustScope(CertificateTrustScope.Append)
    .WithCertificateTrustConfiguration(context =>
    {
        context.EnvironmentVariables[$"{mongoRs.Resource.Name.ToUpperInvariant()}_CERTIFICATE_THUMBPRINT"] = mongoCertificate.Thumbprint;
        return Task.CompletedTask;
    });

var dotNetContainer = builder.AddDockerfileForProject<WebApi>("DotNet-Container")
    .WithReference(mongoRs)
    .WithCertificateAuthorityCollection(mongoCertificateAuthority);

var nodeLocal = builder.AddJavaScriptApp("NodeJS-Local", "../../TypeScript/WebApi")
    .WithHttpEndpoint(targetPort: 3000)
    .WithReference(mongoRs)
    .WithCertificateAuthorityCollection(mongoCertificateAuthority)
    .WithCertificateTrustConfiguration(context =>
    {
        context.EnvironmentVariables["NODE_EXTRA_CA_CERTS"] = context.CertificateBundlePath;
        return Task.CompletedTask;
    });

var nodeContainer = builder.AddDockerfile("NodeJS-Container", "../../TypeScript/WebApi")
    .WithHttpEndpoint(targetPort: 3000)
    .WithReference(mongoRs)
    .WithCertificateAuthorityCollection(mongoCertificateAuthority);

var pythonContainer = builder.AddDockerfile("Python-Container", "../../Python/WebApi")
    .WithHttpEndpoint(targetPort: 8000)
    .WithReference(mongoRs)
    .WithCertificateAuthorityCollection(mongoCertificateAuthority);

var goContainer = builder.AddDockerfile("Go-Container", "../../Go/WebApi")
    .WithHttpEndpoint(targetPort: 8080)
    .WithReference(mongoRs)
    .WithCertificateAuthorityCollection(mongoCertificateAuthority);

builder.Build().Run();
