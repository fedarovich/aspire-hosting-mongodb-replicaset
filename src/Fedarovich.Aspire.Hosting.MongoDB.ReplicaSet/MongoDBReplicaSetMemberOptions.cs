namespace Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

/// <summary>
/// Represents the configuration options for a member of a MongoDB replica set.
/// </summary>
public class MongoDBReplicaSetMemberOptions
{
    /// <summary>
    /// A boolean that identifies an arbiter. A value of <see langword="true"/> indicates that the member is an arbiter.
    /// </summary>
    /// <remarks>
    /// <para>The default value is <see langword="false"/>.</para>
    /// <para>See <see href="https://www.mongodb.com/docs/manual/reference/replica-configuration/#mongodb-rsconf-rsconf.members-n-.arbiterOnly"/> for more details.</para>
    /// </remarks>
    public bool ArbiterOnly { get; set; }

    /// <summary>
    /// A boolean that indicates whether the <c>mongod</c> builds indexes on this member. 
    /// </summary>
    /// <remarks>
    /// <para>The default value is <see langword="true"/>.</para>
    /// <para>
    /// If you set <see cref="BuildIndexes"/> to <see langword="false"/>, you must also set <see cref="Priority" /> to <c>0</c>, otherwise MongoDB will return an error.
    /// </para>
    /// <para>See <see href="https://www.mongodb.com/docs/manual/reference/replica-configuration/#mongodb-rsconf-rsconf.members-n-.buildIndexes" /> for more details.</para>
    /// </remarks>
    public bool BuildIndexes { get; set; } = true;

    /// <summary>
    /// When this value is <see langword="true"/>, the replica set hides this instance and does not include the member in the output of <c>db.hello()</c> or <c>hello</c>.
    /// This prevents read operations (i.e. queries) from ever reaching this host by way of secondary read preference.
    /// </summary>
    /// <remarks>
    /// <para>The default value is <see langword="false"/>.</para>
    /// <para>See <see href="https://www.mongodb.com/docs/manual/reference/replica-configuration/#mongodb-rsconf-rsconf.members-n-.hidden"/> for more details.</para>
    /// </remarks>
    public bool Hidden { get; set; }

    /// <summary>
    /// A number that indicates the relative likelihood of a replica set member to become the primary.
    /// </summary>
    /// <remarks>
    /// <para>Must be a number between <c>0</c> and <c>1000</c> for primary/secondary; <c>0</c> or <c>1</c> for arbiters.</para>
    /// <para>If the value is <see langword="null"/>, the default value of <c>1</c> is used for primary/secondary and <c>0</c> for arbiters.</para>
    /// <para>To increase the likelihood that a member becomes the primary, specify a higher priority value for that member.</para>
    /// <para>To decrease the likelihood that a member becomes the primary, specify a lower priority value for that member.</para>
    /// <para>A member with a priority of <c>0</c> cannot become the primary.</para>
    /// <para>Non-voting members (meaning members that have <see cref="Votes"/> set to <c>0</c>) must have a priority of <c>0</c>.</para>
    /// <para>See <see href="https://www.mongodb.com/docs/manual/reference/replica-configuration/#mongodb-rsconf-rsconf.members-n-.priority"/> for more details.</para>
    /// </remarks>
    public double? Priority
    {
        get;
        set
        {
            if (value is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(value.Value, 0, nameof(value));
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Value, ArbiterOnly ? 1 : 1000, nameof(value));
            }

            field = value;
        }
    }

    /// <summary>
    /// Contains user-defined tag field and value pairs for the replica set member.
    /// </summary>
    public IDictionary<string, string> Tags { get; } = new OrderedDictionary<string, string>();

    /// <summary>
    /// The number of seconds "behind" the primary that this replica set member should "lag".
    /// </summary>
    /// <remarks>
    /// <para>The default value is <c>0</c>.</para>
    /// <para>See <see href="https://www.mongodb.com/docs/manual/reference/replica-configuration/#mongodb-rsconf-rsconf.members-n-.secondaryDelaySecs"/> for more details.</para>
    /// </remarks>
    public int SecondaryDelaySecs
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 0);
            field = value;
        }
    } = 0;

    /// <summary>
    /// The number of votes a server will cast in a replica set election. The number of votes each member has is either 1 or 0, and arbiters always have exactly 1 vote.
    /// </summary>
    /// <remarks>
    /// <para>The default value is <c>1</c>.</para>
    /// <para>Members with <see cref="Priority"/> greater than <c>0</c> cannot have <c>0</c> <see cref="Votes"/>.</para>
    /// <para>Non-voting (i.e. <see cref="Votes"/> is <c>0</c>) members must have <see cref="Priority"/> of <c>0</c>.</para>
    /// </remarks>
    public int Votes
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1);
            field = value;
        }
    } = 1;

    /// <summary>
    /// The additional server addresses of the replica set member.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is used when a member has multiple network interfaces and/or hostnames, and allows the replica set to be configured with multiple addresses for the same member.
    /// </para>
    /// <para>
    /// The replica set will always be configured to access its members on <c>localhost:{Port}</c> and on <c>{name}.dev.internal:{TargetPort}</c>.
    /// You can add additional addresses to this collection if you want the replica set to be configured to access its members on other hostnames and/or network interfaces as well.
    /// Make sure to add the correct X.509 certificate that includes the additional hostnames.
    /// </para>
    /// </remarks>
    public IList<MongoServerAddress> AdditionalServerAddresses { get; } = new List<MongoServerAddress>();
}
