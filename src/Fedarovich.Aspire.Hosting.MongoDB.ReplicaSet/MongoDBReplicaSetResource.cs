namespace Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

public class MongoDBReplicaSetResource(string name, string replicaSetName, ParameterResource keyFile) : Resource(name), 
    IResourceWithEnvironment, 
    IResourceWithArgs,
    IResourceWithWaitSupport,
    IResourceWithConnectionString,
    IResourceWithServiceDiscovery
{
    private readonly OrderedDictionary<MongoDBServerResource, MongoDBReplicaSetMemberOptions> _members = new();

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
    public ReferenceExpression ConnectionStringExpression
    {
        get
        {
            var connectionStringAnnotation = Annotations.OfType<ConnectionStringRedirectAnnotation>()
                .Select(a => a.Resource)
                .ToArray();

            var builder = new ReferenceExpressionBuilder();
            builder.AppendFormatted(new ConnectionStringValueProvider(ReplicaSetName, connectionStringAnnotation));
            return builder.Build();
        }
    }
}
