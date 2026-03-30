#pragma warning disable ASPIRECERTIFICATES001

using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

/// <summary>
/// Provides extension methods for configuring MongoDB replica set resources within a distributed application built using the Aspire framework.
/// </summary>
public static class MongoDBReplicaSetExtensions
{
    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Add a MongoDB replica set resource to the application model.
        /// </summary>
        /// <param name="name">Resource name.</param>
        /// <param name="replicaSetName">Replica set name.</param>
        /// <param name="keyFileParameter">
        /// <para>Parameter resource builder for the content of MongoDB key file, which is used for mutual replica set member authentication.</para>
        /// <para>If <see langword="null"/>, a new resource with a random key will be generated and saved into user secrets.</para>
        /// </param>
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

            string? connectionString = null;
            resourceBuilder.OnConnectionStringAvailable(async (replicaSet, _, ct) =>
            {
                var cs = await replicaSet.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);
                var builder = new MongoUrlBuilder(cs) { AllowInsecureTls = true };
                connectionString = builder.ToString();
            });

            var healthCheckKey = $"{name}_check";
            
            // cache the admin database so it is reused on subsequent calls to the health check
            IMongoDatabase? adminDatabase = null;
            var healthCheckRegistration = new HealthCheckRegistration(
                healthCheckKey,
                sp =>
                {
                    if (adminDatabase is null)
                    {
                        var builder = new MongoUrlBuilder(connectionString ?? throw new InvalidOperationException("Connection string is unavailable"))
                        {
                            ConnectTimeout = TimeSpan.FromSeconds(10),
                            ServerSelectionTimeout = TimeSpan.FromSeconds(10)
                        };
                        adminDatabase = new MongoClient(builder.ToMongoUrl()).GetDatabase("admin");
                    }
                    return new MongoDBReplicaSetHealthCheck(adminDatabase);
                },
                null,
                null,
                null)
            {
                Period = TimeSpan.FromSeconds(10)
            };

            builder.Services.AddHealthChecks().Add(healthCheckRegistration);

            resourceBuilder.WithHealthCheck(healthCheckKey);

            return resourceBuilder;
        }

        /// <summary>
        /// Adds a MongoDB resource to the application model using Percona Server for MongoDB. A container is used for local development.
        /// </summary>
        /// <remarks>
        /// <para>This version of the package defaults to the <c>8.0</c> tag of the <c>percona/percona-server-mongodb</c> container image.</para>
        /// </remarks>
        /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
        /// <param name="port">The host port for MongoDB.</param>
        /// <param name="userName">A parameter that contains the MongoDb server username, or <see langword="null"/> to use a default value.</param>
        /// <param name="password">A parameter that contains the MongoDb server password, or <see langword="null"/> to use a generated password.</param>
        /// <returns></returns>
        public IResourceBuilder<MongoDBServerResource> AddPerconaServerForMongoDB(string name,
            int? port = null,
            IResourceBuilder<ParameterResource>? userName = null,
            IResourceBuilder<ParameterResource>? password = null)
        {
            return builder.AddMongoDB(name, port, userName, password)
                .WithImage(MongoDBContainerImageTags.PerconaImage, MongoDBContainerImageTags.PerconaTag)
                .WithImageRegistry(MongoDBContainerImageTags.PerconaRegistry)
                .WithAnnotation(new MongoDBProcessUserAnnotation(1001, 0));
        }
    }

    extension<T>(IResourceBuilder<T> builder) where T : MongoDBReplicaSetResource
    {
        /// <summary>
        /// Adds a MongoDB server resource as a member of the replica set, applying optional member-specific  configuration.
        /// </summary>
        /// <remarks>
        /// This method ensures that the member is properly configured before being added to the replica set,
        /// including forcing TLS, handling annotations for certificates and certificate authority collections.
        /// </remarks>
        /// <param name="member">
        /// The resource builder for the MongoDB server to add as a member of the replica set.
        /// The resource must have a TCP endpoint annotation with a specified port.
        /// </param>
        /// <param name="configureMember">An optional delegate to configure additional options for the member being added to the replica set.</param>
        /// <returns>The resource builder for the MongoDB replica set, enabling further configuration.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the member has already been added to the replica set, if the member resource does not have a TCP
        /// endpoint annotation, or if the TCP endpoint does not specify a port.
        /// </exception>
        public IResourceBuilder<T> WithMember(
            IResourceBuilder<MongoDBServerResource> member, 
            Action<MongoDBReplicaSetMemberOptions>? configureMember = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(member);

            if (builder.Resource.Members.ContainsKey(member.Resource))
                throw new ArgumentException($"The member '{member.Resource.Name}' has already been added to the replica set.", nameof(member));
            
            var tcpEndpointAnnotation = member.Resource.Annotations.OfType<EndpointAnnotation>().FirstOrDefault(ep => ep.Name == "tcp");
            if (tcpEndpointAnnotation == null)
                throw new ArgumentException($"The member resource '{member.Resource.Name}' must have a TCP endpoint annotation.", nameof(member));

            if (tcpEndpointAnnotation.Port == null)
                throw new ArgumentException($"The TCP endpoint of member resource '{member.Resource.Name}' must have a port specified.", nameof(member));

            member
                .WithArgs("--replSet", builder.Resource.ReplicaSetName)
                .WithArgs("--bind_ip_all")
                .WithKeyFile(builder.Resource.KeyFile)
                .WithTls(builder.Resource)
                .WithEndpointProxySupport(false)
                .WithDirectConnectionHealthChecks();

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

            var options = new MongoDBReplicaSetMemberOptions();
            configureMember?.Invoke(options);
            builder.Resource.AddMember(member.Resource, options);

            return builder;
        }

        /// <summary>
        /// Adds an administration and development platform for MongoDB replica set to the application model using DbGate.
        /// </summary>
        public IResourceBuilder<T> WithDbGate(Action<IResourceBuilder<DbGateContainerResource>>? configureContainer = null, string? containerName = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            containerName ??= "dbgate";

            var dbGateBuilder = builder.ApplicationBuilder.AddDbGate(containerName);

            dbGateBuilder
                .WithEnvironment(ConfigureDbGateContainer)
                .WaitFor(builder);

            configureContainer?.Invoke(dbGateBuilder);

            return builder;

            void ConfigureDbGateContainer(EnvironmentCallbackContext context)
            {
                var mongoDBServer = builder.Resource;

                var name = mongoDBServer.Name;
                var label = $"LABEL_{name}";

                // Multiple WithDbGate calls will be ignored
                if (context.EnvironmentVariables.ContainsKey(label))
                {
                    return;
                }

                // DbGate assumes MongoDB is being accessed over a default Aspire container network and hardcodes the resource address
                // This will need to be refactored once updated service discovery APIs are available
                context.EnvironmentVariables.Add(label, name);
                context.EnvironmentVariables.Add($"URL_{name}", mongoDBServer.ConnectionStringExpression);
                context.EnvironmentVariables.Add($"ENGINE_{name}", "mongo@dbgate-plugin-mongo");
                context.EnvironmentVariables.Add($"USE_SSL_{name}", "true");

                if (context.EnvironmentVariables.GetValueOrDefault("CONNECTIONS") is string { Length: > 0 } connections)
                {
                    context.EnvironmentVariables["CONNECTIONS"] = $"{connections},{name}";
                }
                else
                {
                    context.EnvironmentVariables["CONNECTIONS"] = name;
                }
            }
        }
    }

    extension(IResourceBuilder<MongoDBServerResource> builder)
    {
        private IResourceBuilder<MongoDBServerResource> WithKeyFile(ParameterResource keyFileResource)
        {
            var processUser = builder.Resource.TryGetLastAnnotation<MongoDBProcessUserAnnotation>(out var annotation)
                ? annotation
                : MongoDBProcessUserAnnotation.Default;

            return builder
                .WithContainerFiles(
                    "/keys",
                    async (context, ct) =>
                    {
                        var keyFileContent = await keyFileResource.GetValueAsync(ct);
                        var keyFile = new ContainerFile()
                        {
                            Contents = keyFileContent,
                            Name = "keyfile",
                            Owner = processUser.UserId,
                            Group = processUser.GroupId,
                            Mode = UnixFileMode.UserRead
                        };
                        return [keyFile];
                    },
                    processUser. UserId,
                    processUser.GroupId)
                .WithArgs("--keyFile", "/keys/keyfile");
        }

        private IResourceBuilder<MongoDBServerResource> WithTls(MongoDBReplicaSetResource replicaSet)
        {
            var processUser = builder.Resource.TryGetLastAnnotation<MongoDBProcessUserAnnotation>(out var annotation)
                ? annotation
                : MongoDBProcessUserAnnotation.Default;

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
                            Owner = processUser.UserId,
                            Group = processUser.GroupId,
                            Mode = UnixFileMode.UserRead
                        };
                        
                        return Task.FromResult<IEnumerable<ContainerFileSystemItem>>([certificateFile]);
                    },
                    processUser.UserId,
                    processUser.GroupId)
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

            var healthCheckRegistration = new HealthCheckRegistration(
                healthCheckKey,
                sp =>
                {
                    client ??= new MongoClient(connectionString ?? throw new InvalidOperationException("Connection string is unavailable"));
                    return new MongoDBReplicaSetMemberHealthCheck(client);
                },
                null,
                null,
                null);

            builder.ApplicationBuilder.Services.AddHealthChecks()
                .Add(healthCheckRegistration);

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
            // cache the client so it is reused on subsequent calls to the health check
            IMongoClient? client = null;
            var healthCheckRegistration = new HealthCheckRegistration(
                healthCheckKey,
                sp =>
                {
                    client ??= new MongoClient(connectionString ?? throw new InvalidOperationException("Connection string is unavailable"));
                    return new MongoDBReplicaSetMemberHealthCheck(client, builder.Resource.DatabaseName);
                },
                null,
                null,
                null);
            
            builder.ApplicationBuilder.Services.AddHealthChecks()
                .Add(healthCheckRegistration);

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
