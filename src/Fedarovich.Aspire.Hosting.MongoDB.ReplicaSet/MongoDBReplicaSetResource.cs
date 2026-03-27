namespace Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

public class MongoDBReplicaSetResource(string name, string replicaSetName, ParameterResource keyFile) : Resource(name), 
    IResourceWithEnvironment, 
    IResourceWithArgs,
    IResourceWithWaitSupport,
    IResourceWithConnectionString,
    IResourceWithServiceDiscovery
{
    private readonly List<MongoDBServerResource> _members = new();

    public string ReplicaSetName { get; } = replicaSetName;

    public IReadOnlyList<MongoDBServerResource> Members => _members;

    public ParameterResource KeyFile { get; } = keyFile;

    internal  void AddMember(MongoDBServerResource member) => _members.Add(member);

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
