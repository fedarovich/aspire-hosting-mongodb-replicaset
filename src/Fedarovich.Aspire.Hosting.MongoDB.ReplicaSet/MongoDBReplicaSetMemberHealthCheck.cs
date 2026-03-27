using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;

namespace Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

/// <summary>
/// Performs a health check on a MongoDB replica set member by issuing a ping command to verify connectivity and responsiveness.
/// </summary>
/// <param name="client">The MongoDB client used to connect to the database server. This client must be properly configured to access the desired MongoDB instance.</param>
/// <param name="databaseName">The optional name of the database to use for the health check. If not provided, the health check uses the default
/// database specified in the client's credentials or 'admin' if no credentials are set.
/// </param>
public class MongoDBReplicaSetMemberHealthCheck(IMongoClient client, string? databaseName = null) : IHealthCheck
{
    private const int MaxPingAttempts = 2;

    private static readonly Lazy<BsonDocumentCommand<BsonDocument>> Command = new(() => new(BsonDocument.Parse("{ping:1}")));

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            for (int attempt = 1; attempt <= MaxPingAttempts; attempt++)
            {
                try
                {
                    var dbName = string.IsNullOrEmpty(databaseName)
                        ? client.Settings.Credential.Source ?? "admin"
                        : databaseName;
                    var database = client.GetDatabase(dbName);
                    await database
                        .RunCommandAsync(Command.Value, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    if (MaxPingAttempts == attempt)
                    {
                        throw;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
