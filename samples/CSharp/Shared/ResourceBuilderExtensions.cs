#pragma warning disable ASPIRECERTIFICATES001
#pragma warning disable ASPIREATS001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Shared;

public static class ResourceBuilderExtensions
{
    extension<T>(IResourceBuilder<T> builder) where T : IResourceWithArgs, IResourceWithEnvironment
    {
        [AspireExport("withJavaApplicationCertificateTrust")]
        public IResourceBuilder<T> WithJavaApplicationCertificateTrust()
        {
            return builder
                .WithCertificateTrustConfiguration(context =>
                 {
                     var bundle = context.CreateCustomBundle(
                         (certs, _) => Task.FromResult(certs.ToJavaTrustStoreBytes()));
                     context.EnvironmentVariables["JAVA_TOOL_OPTIONS"] = ReferenceExpression.Create(
                         $"-Djavax.net.ssl.trustStoreType=PKCS12 -Djavax.net.ssl.trustStorePassword= -Djavax.net.ssl.trustStore={bundle}");
                     return Task.CompletedTask;
                 });
        }
    }
}
