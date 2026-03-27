namespace Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

/// <summary>
/// A resource that represents a MongoDB replica set.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="replicaSetName">The name of the MongoDB replica set. It must be unique across all replica sets in the same environment.</param>
/// <param name="keyFile">The parameter resource that represents the contents of the MongoDB key file used to authorize replica set members.</param>
public class MongoDBReplicaSetResource(string name, string replicaSetName, ParameterResource keyFile) : Resource(name), 
    IResourceWithEnvironment, 
    IResourceWithArgs,
    IResourceWithWaitSupport,
    IResourceWithConnectionString,
    IResourceWithServiceDiscovery
{
    private readonly OrderedDictionary<MongoDBServerResource, MongoDBReplicaSetMemberOptions> _members = new();

    private ValueProvider? _valueProvider;

    /// <summary>
    /// Replica set name to be used in the connection string and in the replica set configuration. It must be unique across all replica sets in the same environment.
    /// </summary>
    public string ReplicaSetName { get; } = replicaSetName;

    /// <summary>
    /// Gets a read-only dictionary containing the configuration options for each member of the MongoDB replica set,
    /// indexed by their corresponding server resources.
    /// </summary>
    /// <remarks>
    /// This dictionary is guaranteed to maintain the order in which members were added to the replica set, which can be important for certain configurations and operations.
    /// </remarks>
    public IReadOnlyDictionary<MongoDBServerResource, MongoDBReplicaSetMemberOptions> Members => _members;

    /// <summary>
    /// Gets the parameter resource that represents the contents of the MongoDB key file used to authorize replica set members.
    /// </summary>
    public ParameterResource KeyFile { get; } = keyFile;

    internal  void AddMember(MongoDBServerResource member, MongoDBReplicaSetMemberOptions options) => _members.Add(member, options);

    /// <inheritdoc/>
    public ReferenceExpression ConnectionStringExpression => BuildConnectionStringExpression();

    /// <summary>
    /// Gets the connection URI expression for the MongoDB server.
    /// </summary>
    public ReferenceExpression UriExpression => BuildConnectionStringExpression();

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        if (_members.FirstOrDefault() is { Key: { } member })
        {
            yield return new("Username", member.UserNameReference);

            if (member.PasswordParameter is not null)
            {
                yield return new("Password", ReferenceExpression.Create($"{member.PasswordParameter}"));
            }
        }

        yield return new("Uri", UriExpression);
    }

    private ReferenceExpression BuildConnectionStringExpression()
    {
        _valueProvider ??= new ValueProvider(this);
        var builder = new ReferenceExpressionBuilder();
        builder.AppendFormatted(_valueProvider);
        return builder.Build();
    }

    private class ValueProvider(MongoDBReplicaSetResource replicaSet) : IValueProvider, IManifestExpressionProvider
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

            foreach (var resource in replicaSet.Members.Keys)
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
                ReplicaSetName = replicaSet.ReplicaSetName,
                UseTls = true,
                Username = username,
                Password = password
            };

            return builder.ToString();
        }

        public string ValueExpression => $"{{{replicaSet.Name}.connectionString}}";
    }
}
