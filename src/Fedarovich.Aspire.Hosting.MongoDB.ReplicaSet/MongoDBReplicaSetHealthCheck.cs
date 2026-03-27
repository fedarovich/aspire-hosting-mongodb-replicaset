using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;

namespace Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

public class MongoDBReplicaSetHealthCheck(IMongoDatabase adminDatabase) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var command = new BsonDocument { { "replSetGetStatus", 1 } };

        try
        {
            var result = await adminDatabase.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken).ConfigureAwait(false);

            var isOk = result["ok"].ToBoolean();
            if (!isOk)
                return HealthCheckResult.Unhealthy("Failed to get replica set status.");

            bool hasPrimary = false;
            bool hasHealthy = false;
            bool hasUnhealthy = false;
            var members = result["members"].AsBsonArray;

            foreach (var member in members)
            {
                var state = member["state"].AsInt32;
                var isHealthy = member["health"].ToBoolean() && state is 1 or 2;

                hasPrimary |= state == 1;
                hasHealthy |= isHealthy;
                hasUnhealthy |= !isHealthy;
            }

            if (!hasPrimary)
                return HealthCheckResult.Unhealthy("No primary member found in the replica set.");

            if (!hasHealthy)
                return HealthCheckResult.Unhealthy("No healthy members found in the replica set.");

            if (hasUnhealthy)
                return HealthCheckResult.Degraded("Some members are unhealthy.");

            return HealthCheckResult.Healthy("All members of the replica set are healthy.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to connect to the replica set.", ex);
        }
    }
}
