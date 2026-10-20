using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Wayd.Infrastructure.Hosting;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Web.Api.Mcp;

/// <summary>Registers and maps the hosted MCP server.</summary>
public static class McpServerSetup
{
    /// <summary>The path the MCP server is served at.</summary>
    public const string Path = "/mcp";

    /// <summary>
    /// Registers the MCP server, the tool catalogue it serves, and the prompts generated from the skills,
    /// which every connection is offered whatever toolsets it chose.
    /// </summary>
    public static IServiceCollection AddWaydMcp(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ApiPipeline>();
        services.AddTransient<IStartupFilter>(sp => sp.GetRequiredService<ApiPipeline>());
        services.AddTransient<IStartupFilter, McpFeatureGate>();
        services.AddSingleton<McpToolCatalog>();
        services.AddWaydMcpAuthentication();

        services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "wayd",
                    Title = "Wayd",
                    Version = typeof(McpServerSetup).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0",
                };
                options.ServerInstructions = McpServerInstructions.Text;
                options.Capabilities = new ServerCapabilities { Tools = new ToolsCapability(), Prompts = new PromptsCapability() };
                options.PromptCollection = [.. SkillPrompts.All];
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
    /// Maps the MCP server at <see cref="Path"/> for authenticated callers; <see cref="McpFeatureGate"/> hides it
    /// while the <see cref="FeatureFlags.McpServer"/> flag is off. A request whose <see cref="McpToolFilter"/>
    /// cannot be read is refused with 400.
    /// </summary>
    public static WebApplication MapWaydMcp(this WebApplication app)
    {
        // Resolved here so a malformed value stops the host at startup rather than on the first MCP request.
        if (!app.Services.GetRequiredService<PublicUrl>().IsConfigured && !app.Environment.IsDevelopment())
        {
            app.Logger.LogWarning(
                "{Setting} is not set, so the MCP endpoint's OAuth metadata takes the API's URL from each request. "
                + "Behind a proxy that terminates TLS those URLs read http:// and MCP clients will reject them.",
                PublicUrl.ConfigurationKey);
        }

        app.UseWhen(
            context => context.Request.Path.StartsWithSegments(Path),
            branch => branch.Use(async (context, next) =>
            {
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

        app.MapMcp(Path).RequireAuthorization(McpAuthentication.Policy);
        app.MapWaydAuthorizationServerMetadata();

        return app;
    }
}
