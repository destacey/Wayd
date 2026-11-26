using Microsoft.AspNetCore.Hosting;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Chooses the Wolverine codegen mode a test host boots with.
/// </summary>
/// <remarks>
/// Static where the handler tree was just generated — CI, or a local run with <c>WAYD_STATIC_HANDLERS=true</c> —
/// so the suite dispatches through the exact tree the image ships. Everywhere else the host keeps
/// <c>appsettings.Development.json</c>'s Auto: <c>Wayd.Web.Api.csproj</c> compiles the tree in under the same two
/// switches, so a local build has no pre-generated handlers for Static to load.
/// </remarks>
internal static class HandlerCodegenMode
{
    private static bool UsesStaticHandlers =>
        IsSet("CI") || IsSet("WAYD_STATIC_HANDLERS");

    /// <summary>Sets the host's codegen mode to Static when the handler tree was generated for this run.</summary>
    public static void Apply(IWebHostBuilder builder)
    {
        if (UsesStaticHandlers)
            builder.UseSetting("Wolverine:CodegenMode", "Static");
    }

    private static bool IsSet(string variable) =>
        string.Equals(Environment.GetEnvironmentVariable(variable), "true", StringComparison.OrdinalIgnoreCase);
}
