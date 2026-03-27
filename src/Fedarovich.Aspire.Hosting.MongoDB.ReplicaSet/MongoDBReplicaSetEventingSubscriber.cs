using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using MongoDB.Bson;

namespace Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

internal class MongoDBReplicaSetEventingSubscriber(ResourceNotificationService notification) : IDistributedApplicationEventingSubscriber
{
    public Task SubscribeAsync(
        IDistributedApplicationEventing eventing,
        DistributedApplicationExecutionContext context,
        CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>(async (@event, ct) =>
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

                    if (replicaSet.Members is [])
                    {
                        //@event.Logger.LogError("No members were added to the replica set.");
                        await notification.PublishUpdateAsync(replicaSet, s => s with
                        {
                            State = KnownResourceStates.FailedToStart
                        });
                        return;
                    }

                    await notification.PublishUpdateAsync(replicaSet, s => s with
                    {
                        State = "Initializing replica set..."
                    });

                    var containerNetworkContext = new ValueProviderContext { Network = KnownNetworkIdentifiers.DefaultAspireContainerNetwork };
                    var containerConnectionStrings = await Task.WhenAll(
                        replicaSet.Members.Select(r => r.ConnectionStringExpression.GetValueAsync(containerNetworkContext, ct).AsTask())).ConfigureAwait(false);

                    var localhostNetworkContext = new ValueProviderContext { Network = KnownNetworkIdentifiers.LocalhostNetwork };
                    var localhostConnectionStrings = await Task.WhenAll(
                        replicaSet.Members.Select(r => r.ConnectionStringExpression.GetValueAsync(localhostNetworkContext, ct).AsTask())).ConfigureAwait(false);

                    var connectionStrings = containerConnectionStrings.Zip(localhostConnectionStrings, (container, localhost) => (container, localhost))!;

                    var connectionString = new MongoUrlBuilder(connectionStrings.FirstOrDefault().localhost)
                    {
                        AllowInsecureTls = true,
                        UseTls = true,
                        DirectConnection = true
                    };

                    var mongoClient = new MongoClient(connectionString.ToMongoUrl());
                    var adminDb = mongoClient.GetDatabase("admin");

                    while (true)
                    {
                        if (await InitializeReplicaSetAsync(replicaSet.ReplicaSetName, adminDb, connectionStrings, ct).ConfigureAwait(false))
                        {
                            await notification.PublishUpdateAsync(replicaSet, s => s with
                            {
                                State = KnownResourceStates.Running,
                                StartTimeStamp = DateTime.UtcNow
                            });
                            break;
                        }

                        await Task.Delay(5000, ct);
                    }
                }, ct);

                await notification.PublishUpdateAsync(replicaSet, s => s with
                {
                    State = KnownResourceStates.Starting,
                    StartTimeStamp = DateTime.UtcNow
                });
            }
        });

        return Task.CompletedTask;
    }

    private async Task<bool> InitializeReplicaSetAsync(
        string replicaSetName, 
        IMongoDatabase adminDb, 
        IEnumerable<(string Container, string Localhost)> connectionStrings,
        CancellationToken cancellationToken)
    {
        try
        {
            var initiateCmd = new BsonDocument
            {
                ["replSetInitiate"] = new BsonDocument
                {
                    ["_id"] = replicaSetName,
                    ["members"] = new BsonArray(connectionStrings.Select(ToMember))
                }
            };
            await adminDb.RunCommandAsync<BsonDocument>(initiateCmd, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoCommandException ex)
        {
            if (ex.CodeName != "AlreadyInitialized")
            {
                return true;
            }
        }
        catch (Exception)
        {
            // TODO: Log
        }

        return false;

        static BsonDocument ToMember((string Container, string Localhost) connectionStrings, int index)
        {
            var containerUrl = new MongoUrl(connectionStrings.Container);
            var localhostUrl = new MongoUrl(connectionStrings.Localhost);

            return new BsonDocument
            {
                ["_id"] = index,
                ["host"] = $"{containerUrl.Server.Host}:{containerUrl.Server.Port}",
                ["horizons"] = new BsonDocument
                {
                    ["localhost"] = $"{localhostUrl.Server.Host}:{localhostUrl.Server.Port}"
                }
            };
        }
    }
}
