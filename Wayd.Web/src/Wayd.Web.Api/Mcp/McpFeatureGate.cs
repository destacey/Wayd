using Microsoft.FeatureManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure.Hosting;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Answers 404 for <see cref="McpServerSetup.Path"/>, its <see cref="McpAuthentication.ResourceMetadataPath"/>, and the
/// authorization server's metadata and endpoints (<see cref="AuthorizationServerMetadata"/>) while the
/// <see cref="FeatureFlags.McpServer"/> flag is off, and 503 outside Development while no <see cref="PublicUrl"/> is
/// configured.
/// </summary>
/// <remarks>
/// <para>
/// Registered as an <see cref="IStartupFilter"/> so it runs ahead of authentication: behind it, a caller without
/// credentials would be challenged, learning that the endpoint exists and being pointed at metadata that is not served.
/// </para>
/// <para>
/// Without a configured public URL the issuer, the token audience and the metadata are read from the request's
/// <c>Host</c> header, which a deployment that accepts any host lets the caller choose. Development is exempt because
/// it is reached directly, with nothing in front to vary the host.
/// </para>
/// </remarks>
public sealed class McpFeatureGate(IHostEnvironment environment, PublicUrl publicUrl) : IStartupFilter
{
    private readonly bool _requiresPublicUrl = !environment.IsDevelopment() && !publicUrl.IsConfigured;

    /// <summary>Whether a request to <paramref name="path"/> is one this gate decides.</summary>
    private static bool Covers(PathString path) =>
        path.StartsWithSegments(McpServerSetup.Path)
        || path.StartsWithSegments(McpAuthentication.ResourceMetadataPath)
        || path.StartsWithSegments(AuthorizationServerMetadata.Path)
        || path.StartsWithSegments(AuthorizationServerMetadata.EndpointsPath);

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseWhen(
            context => Covers(context.Request.Path),
            branch => branch.Use(async (context, nextMiddleware) =>
            {
                if (!await context.RequestServices.GetRequiredService<IFeatureManager>().IsEnabledAsync(FeatureFlags.Names.McpServer))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                if (_requiresPublicUrl)
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    return;
                }

                await nextMiddleware(context);
            }));
        next(app);
    };
}
