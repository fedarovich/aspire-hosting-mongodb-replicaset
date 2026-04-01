#pragma warning disable ASPIRECERTIFICATES001
#pragma warning disable ASPIREATS001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Shared;

public static class DistributedApplicationBuilderExtensions
{
    extension(IDistributedApplicationBuilder builder)
    {
        [AspireExportIgnore]
        public IResourceBuilder<ContainerResource> AddDockerfileForProject<TProject>(string name) where TProject : IProjectMetadata, new()
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            var projectMetadata = new TProject();
            return builder.AddDockerfileForProject(name, projectMetadata.ProjectPath);
        }

        [AspireExport("addDockerfileForProject")]
        public IResourceBuilder<ContainerResource> AddDockerfileForProject(string name, string projectPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentException.ThrowIfNullOrEmpty(projectPath);

            projectPath = Path.GetFullPath(projectPath, builder.AppHostDirectory);

            var projectDirectory = Path.GetDirectoryName(projectPath)!;
            var contextDirectory = Path.GetDirectoryName(projectDirectory)!;
            var dockerfile = Path.Combine(projectDirectory, "Dockerfile");
            return builder.AddDockerfile(name, contextDirectory, dockerfile)
                .WithEnvironment("ASPNETCORE_URLS", "https://+:443;http://+:80")
                .WithHttpEndpoint(targetPort: 80)
                .WithHttpsEndpoint(targetPort: 443)
                .WithHttpsDeveloperCertificate()
                .WithDeveloperCertificateTrust(true)
                .WithCertificateTrustScope(CertificateTrustScope.System)
                .WithHttpsCertificateConfiguration(context =>
                {
                    context.EnvironmentVariables["ASPNETCORE_Kestrel__Certificates__Default__Path"] = context.PfxPath;
                    return Task.CompletedTask;
                });
        }
    }
}
