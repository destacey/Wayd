namespace Wayd.ArchitectureTests.Helpers;

/// <summary>
/// Shared project-naming conventions used by the architecture tests, centralized so the same allowances
/// are applied consistently across every rule.
/// </summary>
public static class ProjectConventions
{
    /// <summary>
    /// Projects under a <c>tests/</c> folder that are NOT test projects and are therefore exempt from the
    /// test-project rules (xUnit usage, <c>.Tests</c>/<c>.IntegrationTests</c> suffix) and the source-project
    /// rules (living under <c>src/</c>):
    /// <list type="bullet">
    ///   <item><description><c>Wayd.Tests.Shared</c> — shared test utilities.</description></item>
    ///   <item><description><c>Wayd.Tests.Containers</c> — how integration suites start their SQL Server
    ///   container. Referenced only by integration projects, so the unit projects never depend on
    ///   Testcontainers.</description></item>
    ///   <item><description><c>Wayd.TestData.Core</c> — the Bogus-only <c>PrivateConstructorFaker&lt;T&gt;</c>
    ///   base. It contains no tests and references no test framework. Domain fakers live in each domain's own
    ///   <c>Domain.Tests</c> project, so no other faker library is exempt.</description></item>
    /// </list>
    /// </summary>
    public static bool IsExemptSupportProject(string projectName) =>
        projectName == "Wayd.Tests.Shared" ||
        projectName == "Wayd.Tests.Containers" ||
        projectName == "Wayd.TestData.Core";
}
