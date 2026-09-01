using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Wayd.Infrastructure;

/// <summary>
/// Identifies hosts that are built only to be introspected rather than to serve requests, so that
/// startup work touching the database can be skipped.
/// </summary>
public static class HostIntrospection
{
    /// <summary>
    /// True when this host exists only to be inspected:
    /// <list type="bullet">
    /// <item><see cref="EF.IsDesignTime"/> — the EF Core tooling (migrations add/remove/update).</item>
    /// <item><c>WAYD_SKIP_DB_INIT</c> — NSwag boots the real app to read the OpenAPI document on
    /// every Debug build. <see cref="EF.IsDesignTime"/> is false there. Honoured as an environment
    /// variable (set by the NSwag MSBuild target) and as a host setting, so integration tests can opt
    /// out per-host via <c>UseSetting</c> without a process-wide env var leaking into sibling hosts.</item>
    /// </list>
    /// </summary>
    public static bool SkipsDatabaseInitialization(IConfiguration config) =>
        EF.IsDesignTime ||
        string.Equals(Environment.GetEnvironmentVariable("WAYD_SKIP_DB_INIT"), "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(config["WAYD_SKIP_DB_INIT"], "true", StringComparison.OrdinalIgnoreCase);
}
