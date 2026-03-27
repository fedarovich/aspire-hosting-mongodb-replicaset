namespace Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

internal class ConnectionStringValueProvider(string replicaSetName, IReadOnlyList<IResourceWithConnectionString> resources) : IValueProvider, IManifestExpressionProvider
{
    public ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default)
    {
        return GetValueAsync(new ValueProviderContext(), cancellationToken);
    }

    public async ValueTask<string?> GetValueAsync(ValueProviderContext context, CancellationToken cancellationToken = default)
    {
        var servers = new List<MongoServerAddress>();
        string? username = null;
        string? password = null;

        foreach (var resource in resources)
        {
            var connectionString = await resource.ConnectionStringExpression.GetValueAsync(context, cancellationToken);
            var url = new MongoUrl(connectionString);
            servers.Add(url.Server);
            username ??= url.Username;
            password ??= url.Password;
        }

        var builder = new MongoUrlBuilder
        {
            Servers = servers,
            ReplicaSetName = replicaSetName,
            UseTls = true,
            Username = username,
            Password = password
        };

        return builder.ToString();
    }

    // TODO: Generate a correct expression that uses all connection strings of the members instead of just the first one.
    public string ValueExpression => resources[0].ValueExpression;
}
