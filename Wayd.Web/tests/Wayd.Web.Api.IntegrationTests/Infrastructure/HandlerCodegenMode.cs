namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Chooses the Wolverine codegen mode a test host boots with.
/// </summary>
/// <remarks>
/// <para>
/// Static where the handler tree was just generated — CI, or a local run with <c>WAYD_STATIC_HANDLERS=true</c> —
/// so the suite dispatches through the exact tree the image ships. Everywhere else the host keeps
/// <c>appsettings.Development.json</c>'s Auto: <c>Wayd.Web.Api.csproj</c> compiles the tree in under the same two
/// switches, so a local build has no pre-generated handlers for Static to load.
/// </para>
/// <para>
/// Set as an environment variable because <c>AddWaydWolverine</c> reads the mode while the host is being built,
/// where <c>UseSetting</c> does not out-rank the json file that sets it.
/// </para>
/// </remarks>
internal static class HandlerCodegenMode
{
    private const string Key = "Wolverine__CodegenMode";

    private static bool UsesStaticHandlers =>
        IsSet("CI") || IsSet("WAYD_STATIC_HANDLERS");

    public static void Apply()
    {
        if (UsesStaticHandlers)
        {
            Environment.SetEnvironmentVariable(Key, "Static");
        }
    }

    public static void Clear()
    {
        if (UsesStaticHandlers)
        {
            Environment.SetEnvironmentVariable(Key, null);
        }
    }

    private static bool IsSet(string variable) =>
        string.Equals(Environment.GetEnvironmentVariable(variable), "true", StringComparison.OrdinalIgnoreCase);
}
