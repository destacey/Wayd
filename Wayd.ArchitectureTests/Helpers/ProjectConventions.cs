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
    ///   <item><description>Test-data / faker libraries whose name contains a <c>TestData</c> segment —
    ///   Bogus-only, e.g. <c>Wayd.Organization.TestData</c> (suffix) and <c>Wayd.TestData.Core</c> (mid-name).
    ///   They are consumed by test projects (and the wayd-data dev tool) but contain no tests and reference no
    ///   test framework, and they live under <c>tests/</c> to signal they are test-only.</description></item>
    /// </list>
    /// </summary>
    public static bool IsExemptSupportProject(string projectName) =>
        projectName == "Wayd.Tests.Shared" ||
        IsTestDataLibrary(projectName);

    /// <summary>True for a Bogus-only test-data/faker library, identified by a <c>TestData</c> name segment.</summary>
    public static bool IsTestDataLibrary(string projectName) =>
        projectName.EndsWith(".TestData", StringComparison.Ordinal) ||
        projectName.Contains(".TestData.", StringComparison.Ordinal);
}
