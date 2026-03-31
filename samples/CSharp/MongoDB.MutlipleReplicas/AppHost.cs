using Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;
using Projects;

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

builder.Build().Run();
