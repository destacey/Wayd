using System.Reflection;
using Microsoft.FeatureManagement;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Web.Api.Mcp;

/// <summary>Registers and maps the hosted MCP server.</summary>
public static class McpServerSetup
{
    /// <summary>The path the MCP server is served at.</summary>
    public const string Path = "/mcp";

    /// <summary>Registers the MCP server and the tool catalogue it serves.</summary>
    public static IServiceCollection AddWaydMcp(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ApiPipeline>();
        services.AddTransient<IStartupFilter>(sp => sp.GetRequiredService<ApiPipeline>());
        services.AddSingleton<McpToolCatalog>();

        services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "wayd",
                    Title = "Wayd",
                    Version = typeof(McpServerSetup).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0",
                };
                options.ServerInstructions = McpServerInstructions.Text;
                options.Capabilities = new ServerCapabilities { Tools = new ToolsCapability() };
            })
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
                options.ConfigureSessionOptions = async (context, serverOptions, _) =>
                {
                    var tools = await context.RequestServices.GetRequiredService<McpToolCatalog>().GetTools(McpToolFilter.For(context));
                    var collection = new McpServerPrimitiveCollection<McpServerTool>();
                    foreach (var tool in tools)
                        collection.Add(tool);
                    serverOptions.ToolCollection = collection;
                };
            });

        return services;
    }

    /// <summary>
    /// Maps the MCP server at <see cref="Path"/>, for authenticated callers while the
    /// <see cref="FeatureFlags.McpServer"/> flag is on, and answers 404 while it is off. A request whose
    /// <see cref="McpToolFilter"/> cannot be read is refused with 400.
    /// </summary>
    public static WebApplication MapWaydMcp(this WebApplication app)
    {
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments(Path),
            branch => branch.Use(async (context, next) =>
            {
                if (!await context.RequestServices.GetRequiredService<IFeatureManager>().IsEnabledAsync(FeatureFlags.Names.McpServer))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                var filter = McpToolFilter.From(context.Request);
                if (filter.IsFailure)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync(filter.Error, context.RequestAborted);
                    return;
                }
                McpToolFilter.Store(context, filter.Value);

                await next(context);
            }));

        app.MapMcp(Path).RequireAuthorization();

        return app;
    }
}
