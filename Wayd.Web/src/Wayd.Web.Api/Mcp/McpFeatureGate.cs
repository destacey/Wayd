using Microsoft.FeatureManagement;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Answers 404 for <see cref="McpServerSetup.Path"/> and its <see cref="McpAuthentication.ResourceMetadataPath"/>
/// while the <see cref="FeatureFlags.McpServer"/> flag is off.
/// </summary>
/// <remarks>
/// Registered as an <see cref="IStartupFilter"/> so it runs ahead of authentication: behind it, a caller without
/// credentials would be challenged, learning that the endpoint exists and being pointed at metadata that is not served.
/// </remarks>
public sealed class McpFeatureGate : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments(McpServerSetup.Path)
                || context.Request.Path.StartsWithSegments(McpAuthentication.ResourceMetadataPath),
            branch => branch.Use(async (context, nextMiddleware) =>
            {
                if (!await context.RequestServices.GetRequiredService<IFeatureManager>().IsEnabledAsync(FeatureFlags.Names.McpServer))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                await nextMiddleware(context);
            }));
        next(app);
    };
}
