#pragma warning disable ASPIRECERTIFICATES001

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Aspire.Hosting.Lifecycle;

namespace Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

public static class MongoDBReplicaSetExtensions
{
    private const int UserId = 999;
    private const int GroupId = 999;

    extension(IDistributedApplicationBuilder builder)
    {
        public IResourceBuilder<MongoDBReplicaSetResource> AddMongoDBReplicaSet(string name,
            string replicaSetName = "rs0",
            IResourceBuilder<ParameterResource>? keyFileParameter = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(replicaSetName);

            builder.Services.TryAddEventingSubscriber<MongoDBReplicaSetEventingSubscriber>();

            var keyFile = keyFileParameter?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-keyfile", special: false);

            var resource = new MongoDBReplicaSetResource(name, replicaSetName, keyFile);
            var resourceBuilder = builder.AddResource(resource)
                .WithIconName("LayerDiagonal", IconVariant.Regular)
                .WithInitialState(new CustomResourceSnapshot
                {
                    ResourceType = "MongoDBReplicaSet",
                    CreationTimeStamp = DateTime.UtcNow,
                    State = KnownResourceStates.NotStarted,
                    Properties = []
                });

            return resourceBuilder;
        }
    }

    extension(IResourceBuilder<MongoDBReplicaSetResource> builder)
    {
        public IResourceBuilder<MongoDBReplicaSetResource> WithMember(IResourceBuilder<MongoDBServerResource> member)
        {
            member
                .WithArgs("--replSet", builder.Resource.ReplicaSetName)
                .WithArgs("--bind_ip_all")
                .WithKeyFile(builder.Resource.KeyFile)
                .WithTls(builder.Resource)
                .WithEndpointProxySupport(false)
                .WithDirectConnectionHealthChecks()
                ;

            member.OnInitializeResource((_, _, _) =>
            {
                var certificateAnnotation = builder.Resource.Annotations
                    .OfType<HttpsCertificateAnnotation>()
                    .FirstOrDefault();

                if (certificateAnnotation is not null)
                {
                    member.WithAnnotation(certificateAnnotation, ResourceAnnotationMutationBehavior.Replace);
                }

                var certificateAuthorityCollectionAnnotation = builder.Resource.Annotations
                    .OfType<CertificateAuthorityCollectionAnnotation>()
                    .FirstOrDefault();
                if (certificateAuthorityCollectionAnnotation is not null)
                {
                    member.WithAnnotation(certificateAuthorityCollectionAnnotation, ResourceAnnotationMutationBehavior.Replace);
                }

                return Task.CompletedTask;
            });

            builder.WithChildRelationship(member);
            builder.WaitForStart(member);
            builder.WithAnnotation(new ConnectionStringRedirectAnnotation(member.Resource));

            builder.Resource.AddMember(member.Resource);

            return builder;
        }
    }

    extension(IResourceBuilder<MongoDBServerResource> builder)
    {
        private IResourceBuilder<MongoDBServerResource> WithKeyFile(ParameterResource keyFileResource) => builder
            .WithContainerFiles(
                "/keys",
                async (context, ct) =>
                {
                    var keyFileContent = await keyFileResource.GetValueAsync(ct);
                    var keyFile = new ContainerFile()
                    {
                        Contents = keyFileContent,
                        Name = "keyfile",
                        Owner = UserId,
                        Group = GroupId,
                        Mode = UnixFileMode.UserRead
                    };
                    return [keyFile];
                },
                UserId,
                GroupId)
            .WithArgs("--keyFile", "/keys/keyfile");

        private IResourceBuilder<MongoDBServerResource> WithTls(MongoDBReplicaSetResource replicaSet)
        {
            return builder
                .OnInitializeResource((_, _, _) =>
                {
                    if (replicaSet.TryGetLastAnnotation<HttpsCertificateAnnotation>(out var certificateAnnotation))
                    {
                        builder.WithAnnotation(certificateAnnotation, ResourceAnnotationMutationBehavior.Replace);
                    }

                    if (replicaSet.TryGetLastAnnotation<CertificateAuthorityCollectionAnnotation>(out var certificateAuthorityCollectionAnnotation))
                    {
                        builder.WithAnnotation(certificateAuthorityCollectionAnnotation, ResourceAnnotationMutationBehavior.Replace);
                    }

                    return Task.CompletedTask;
                })
                .WithContainerFiles(
                    "/cert",
                    (context, _) =>
                    {
                        var effectiveAnnotation = new HttpsCertificateAnnotation();
                        if (context.Model.TryGetLastAnnotation<HttpsCertificateAnnotation>(out var annotation))
                        {
                            effectiveAnnotation = annotation;
                        }

                        X509Certificate2? certificate = effectiveAnnotation.Certificate;
                        if (certificate is null)
                        {
                            var developerCertificateService = context.ServiceProvider.GetRequiredService<IDeveloperCertificateService>();
                            if (effectiveAnnotation.UseDeveloperCertificate.GetValueOrDefault(developerCertificateService.UseForHttps))
                            {
                                certificate = developerCertificateService.Certificates.FirstOrDefault();
                            }
                        }

                        if (certificate is null)
                            throw new DistributedApplicationException("No X509 certificate is configured for the MongoDB replica set.");

                        var stringBuilder = new StringBuilder();
                        stringBuilder.AppendLine(GetPrivateKeyPem(certificate));
                        stringBuilder.AppendLine(certificate.ExportCertificatePem());
                        var certificateFile = new ContainerFile
                        {
                            Name = "server-cert.pem",
                            Contents = stringBuilder.ToString(),
                            Owner = 999,
                            Group = 999,
                            Mode = UnixFileMode.UserRead
                        };
                        
                        return Task.FromResult<IEnumerable<ContainerFileSystemItem>>([certificateFile]);
                    },
                    UserId,
                    GroupId)
                .WithArgs(
                    "--tlsMode", "requireTLS",
                    "--tlsCertificateKeyFile", "/cert/server-cert.pem",
                    "--setParameter", "tlsUseSystemCA=true",
                    "--tlsAllowConnectionsWithoutCertificates");
        }
    
        private IResourceBuilder<MongoDBServerResource> WithDirectConnectionHealthChecks()
        {
            builder.RemoveHealthChecks();

            string? connectionString = null;

            builder.OnConnectionStringAvailable(
                async (serverResource, _, ct) => connectionString = await serverResource.GetDirectConnectionStringAsync(ct).ConfigureAwait(false));

            builder.OnInitializeResource((s, _, _) =>
            {
                var databases = s.Databases;

                foreach (var (dbResourceName, _) in databases)
                {
                    builder.ApplicationBuilder.CreateResourceBuilder<MongoDBDatabaseResource>(dbResourceName)
                        .WithDirectConnectionHealthChecks();
                }

                return Task.CompletedTask;
            });

            var healthCheckKey = $"{builder.Resource.Name}_direct_check";
            // cache the client so it is reused on subsequent calls to the health check
            IMongoClient? client = null;
            builder.ApplicationBuilder.Services.AddHealthChecks()
                .AddMongoDb(
                    sp => client ??= new MongoClient(connectionString ?? throw new InvalidOperationException("Connection string is unavailable")),
                    name: healthCheckKey);

            builder.WithHealthCheck(healthCheckKey);

            return builder;
        }
    }

    extension(IResourceBuilder<MongoDBDatabaseResource> builder)
    {
        private IResourceBuilder<MongoDBDatabaseResource> WithDirectConnectionHealthChecks()
        {
            builder.RemoveHealthChecks();

            string? connectionString = null;

            builder.OnConnectionStringAvailable(
                async (dbResource, _, ct) => connectionString = await dbResource.GetDirectConnectionStringAsync(ct).ConfigureAwait(false));

            var healthCheckKey = $"{builder.Resource.Name}_direct_check";
            // cache the database client so it is reused on subsequent calls to the health check
            IMongoDatabase? database = null;
            builder.ApplicationBuilder.Services.AddHealthChecks()
                .AddMongoDb(
                    sp => database ??=
                        new MongoClient(connectionString ?? throw new InvalidOperationException("Connection string is unavailable"))
                            .GetDatabase(builder.Resource.DatabaseName),
                    name: healthCheckKey);

            builder.WithHealthCheck(healthCheckKey);

            return builder;
        }
    }

    extension<T>(T resource) where T : IResourceWithConnectionString
    {
        private async Task<string?> GetDirectConnectionStringAsync(CancellationToken ct)
        {
            var connectionString = await resource.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);

            if (connectionString == null)
            {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{resource.Name}' resource but the connection string was null.");
            }

            var urlBuilder = new MongoUrlBuilder(connectionString)
            {
                DirectConnection = true,
                AllowInsecureTls = true,
                UseTls = true
            };
            connectionString = urlBuilder.ToString();

            return connectionString;
        }
    }

    extension<T>(IResourceBuilder<T> builder) where T : IResource
    {
        private IResourceBuilder<T> RemoveHealthChecks()
        {
            foreach (var annotation in builder.Resource.Annotations.OfType<HealthCheckAnnotation>().ToList())
            {
                builder.Resource.Annotations.Remove(annotation);
                builder.ApplicationBuilder.Services.Configure<HealthCheckServiceOptions>(options =>
                {
                    var mongoDbServerHealthCheck = options.Registrations.FirstOrDefault(x => x.Name == annotation.Key);
                    if (mongoDbServerHealthCheck is not null)
                    {
                        options.Registrations.Remove(mongoDbServerHealthCheck);
                    }
                });
            }

            return builder;
        }
    }

    private static string GetPrivateKeyPem(X509Certificate2 certificate)
    {
        if (certificate.GetRSAPrivateKey() is { } rsa)
            return rsa.ExportPkcs8PrivateKeyPem();
        if (certificate.GetDSAPrivateKey() is { } dsa)
            return dsa.ExportPkcs8PrivateKeyPem();
        if (certificate.GetECDsaPrivateKey() is { } ecdsa)
            return ecdsa.ExportPkcs8PrivateKeyPem();
        if (certificate.GetECDiffieHellmanPrivateKey() is { } ecdh)
            return ecdh.ExportPkcs8PrivateKeyPem();
        throw new InvalidOperationException("Certificate does not have a supported private key.");
    }
}
