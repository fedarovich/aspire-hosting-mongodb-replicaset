namespace Fedarovich.Aspire.Hosting.MongoDB.ReplicaSet;

/// <summary>
/// Contains the User ID and Group ID for the linux user that the MongoDB process runs as.
/// </summary>
/// <param name="UserId">
/// <para>The User ID of the MongoDB process.</para>
/// <para>The default value is <c>999</c> to match the UID in the default MongoDB image (<c>library/mongo</c>).</para>
/// </param>
/// <param name="GroupId">
/// <para>The Group ID of the MongoDB process.</para>
/// <para>The default value is <c>999</c> to match the GID in the default MongoDB image (<c>library/mongo</c>).</para>
/// </param>
/// <remarks>
/// This annotation is used to set the correct file permissions for the certificate and key files.
/// </remarks>
public record MongoDBProcessUserAnnotation(int UserId = 999, int GroupId = 999) : IResourceAnnotation
{
    internal static readonly MongoDBProcessUserAnnotation Default = new();
}
