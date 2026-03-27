using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

internal class MongoDBReplicaSetEventingSubscriber(
    ResourceNotificationService notification,
    ILogger<MongoDBReplicaSetEventingSubscriber> logger
) : IDistributedApplicationEventingSubscriber
{
    public Task SubscribeAsync(
        IDistributedApplicationEventing eventing,
        DistributedApplicationExecutionContext context,
        CancellationToken cancellationToken)
    {
        var containerNetworkContext = new ValueProviderContext { ExecutionContext = context, Network = KnownNetworkIdentifiers.DefaultAspireContainerNetwork };
        var localhostNetworkContext = new ValueProviderContext { ExecutionContext = context, Network = KnownNetworkIdentifiers.LocalhostNetwork };

        eventing.Subscribe<BeforeStartEvent>((@event, ct) =>
        {
            foreach (var replicaSet in @event.Model.Resources.OfType<MongoDBReplicaSetResource>())
            {
                _ = Task.Run(async () =>
                {
                    await notification.PublishUpdateAsync(replicaSet, s => s with
                    {
                        State = KnownResourceStates.Waiting
                    });

                    await notification.WaitForDependenciesAsync(replicaSet, ct);

                    if (replicaSet.Members.Count == 0)
                    {
                        logger.LogCritical("No members were added to the replica set.");
                        await notification.PublishUpdateAsync(replicaSet, s => s with
                        {
                            State = KnownResourceStates.FailedToStart
                        });
                        return;
                    }

                    var members = await Task.WhenAll(replicaSet.Members.Select(GetMemberInfoAsync)).ConfigureAwait(false);

                    await eventing.PublishAsync(new ConnectionStringAvailableEvent(replicaSet, context.ServiceProvider), ct);

                    await notification.PublishUpdateAsync(replicaSet, s => s with
                    {
                        State = KnownResourceStates.Starting
                    });

                    var connectionString = new MongoUrlBuilder(members[0].LocalhostConnectionString)
                    {
                        AllowInsecureTls = true,
                        UseTls = true,
                        DirectConnection = true
                    };

                    var mongoClient = new MongoClient(connectionString.ToMongoUrl());
                    var adminDb = mongoClient.GetDatabase("admin");

                    bool isRunning = false;

                    while (true)
                    {
                        if (await InitializeReplicaSetAsync(replicaSet.ReplicaSetName, adminDb, members, ct).ConfigureAwait(false) && !isRunning)
                        {
                            await notification.PublishUpdateAsync(replicaSet, s => s with
                            {
                                State = KnownResourceStates.Running,
                                StartTimeStamp = DateTime.UtcNow
                            });
                            isRunning = true;
                        }

                        await Task.Delay(10_000, ct);
                    }

                    async Task<ReplicaSetMemberInfo> GetMemberInfoAsync(KeyValuePair<MongoDBServerResource, MongoDBReplicaSetMemberOptions> pair, int id)
                    {
                        var (server, options) = pair;
                        var containerConnectionStringTask = server.ConnectionStringExpression.GetValueAsync(containerNetworkContext, ct).AsTask();
                        var localhostConnectionStringTask = server.ConnectionStringExpression.GetValueAsync(localhostNetworkContext, ct).AsTask();
                        await Task.WhenAll(containerConnectionStringTask, localhostConnectionStringTask).ConfigureAwait(false);
                        return new ReplicaSetMemberInfo(
                            id,
                            server,
                            options,
                            containerConnectionStringTask.Result!,
                            localhostConnectionStringTask.Result!);
                    }
                }, ct);
            }

            return Task.CompletedTask;
        });

        return Task.CompletedTask;
    }

    private async Task<bool> InitializeReplicaSetAsync(
        string replicaSetName, 
        IMongoDatabase adminDb, 
        ReplicaSetMemberInfo[] members,
        CancellationToken cancellationToken)
    {
        try
        {
            var initiateCmd = new BsonDocument
            {
                ["replSetInitiate"] = new BsonDocument
                {
                    ["_id"] = replicaSetName,
                    ["members"] = new BsonArray(members.Select(ToBson))
                }
            };
            await adminDb.RunCommandAsync<BsonDocument>(initiateCmd, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoCommandException ex)
        {
            if (ex.CodeName == "AlreadyInitialized")
            {
                logger.LogDebug("The replica set is already initialized.");
                return true;
            }

            logger.LogError(ex, "Failed to initialize the replica set.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while initializing the replica set.");
        }

        return false;

        static BsonDocument ToBson(ReplicaSetMemberInfo memberInfo)
        {
            var options = memberInfo.Options;

            var containerUrl = new MongoUrl(memberInfo.ContainerConnectionString);
            var localhostUrl = new MongoUrl(memberInfo.LocalhostConnectionString);

            var horizons = new BsonDocument
            {
                ["localhost"] = $"{localhostUrl.Server.Host}:{localhostUrl.Server.Port}"
            };

            foreach (var serverAddress in options.AdditionalServerAddresses)      
            {
                horizons.Add(serverAddress.Host, $"{serverAddress.Host}:{serverAddress.Port}");
            }

            var memberConfig = new BsonDocument
            {
                ["_id"] = memberInfo.Id,
                ["host"] = $"{containerUrl.Server.Host}:{containerUrl.Server.Port}",
                ["horizons"] = horizons
            };

            if (options.ArbiterOnly)
            {
                memberConfig["arbiterOnly"] = true;
            }

            if (!options.BuildIndexes)
            {
                memberConfig["buildIndexes"] = false;
            }

            if (options.Hidden)
            {
                memberConfig["hidden"] = true;
            }

            if (options.Priority.HasValue)
            {
                memberConfig["priority"] = options.Priority.Value;
            }

            if (options.SecondaryDelaySecs > 0)
            {
                memberConfig["secondaryDelaySecs"] = options.SecondaryDelaySecs;
            }

            if (options.Votes != 1)
            {
                memberConfig["votes"] = options.Votes;
            }

            if (options.Tags.Count > 0)
            {
                memberConfig["tags"] = new BsonDocument(options.Tags.Select(kv => new KeyValuePair<string, object>(kv.Key, kv.Value)));
            }

            return memberConfig;
        }
    }

    private record ReplicaSetMemberInfo(
        int Id,
        MongoDBServerResource Server, 
        MongoDBReplicaSetMemberOptions Options, 
        string ContainerConnectionString, 
        string LocalhostConnectionString);
}
