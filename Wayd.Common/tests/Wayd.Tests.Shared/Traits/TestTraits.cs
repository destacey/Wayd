namespace Wayd.Tests.Shared.Traits;

/// <summary>
/// Trait NAMES for the three independent axes a test can be selected on. Kept as constants so a filter and
/// the attribute that satisfies it cannot drift apart — <c>dotnet test --filter</c> fails OPEN (an unknown
/// name or value matches nothing and still exits zero for that assembly), so a typo silently runs fewer
/// tests rather than erroring.
/// </summary>
public static class TestTraits
{
    /// <summary>How the test runs and what it needs. Applied per-assembly, not per-class — see below.</summary>
    public const string Category = "Category";

    /// <summary>When the test runs: a curated fast pass versus the full body of tests.</summary>
    public const string Suite = "Suite";

    /// <summary>What the test covers, cutting across projects.</summary>
    public const string Area = "Area";
}

/// <summary>
/// Values for <see cref="TestTraits.Category"/>.
/// <para>
/// These are applied at ASSEMBLY level from Directory.Build.props, derived from whether the project
/// references Testcontainers — the dependency that actually requires a Docker daemon. Do not apply them by
/// hand to a class: a per-class copy is a second source of truth that drifts from what the project needs,
/// which is exactly how the previous per-class "Docker" trait ended up on six classes in one of the four
/// Docker-dependent projects.
/// </para>
/// </summary>
public static class TestCategories
{
    /// <summary>No external dependency: runs anywhere, needs no Docker daemon or network.</summary>
    public const string Unit = "Unit";

    /// <summary>Needs a real backing service (a Testcontainers SQL Server), so it needs Docker.</summary>
    public const string Integration = "Integration";
}

/// <summary>
/// Values for <see cref="TestTraits.Suite"/>. Absence of a Suite trait means "regression" — the default body
/// of tests — so only the curated fast pass is tagged.
/// </summary>
public static class TestSuites
{
    /// <summary>
    /// A deliberately small, fast pass that proves the system is fundamentally working. Membership is
    /// curated, not comprehensive: a smoke suite is only useful while it stays quick, so add a test here
    /// only when its failure would mean "stop and look now" rather than "something regressed".
    /// </summary>
    public const string Smoke = "Smoke";
}

/// <summary>
/// Values for <see cref="TestTraits.Area"/> — what a test covers, independent of how it runs. This is the
/// axis the project layout cannot express, since a single area is exercised from domain, application and
/// integration projects alike.
/// <para>
/// Tag opportunistically as tests are touched rather than in a bulk pass: a partial map is still useful,
/// while a hurried retag produces an inconsistent one, which is worse than none.
/// </para>
/// </summary>
public static class TestAreas
{
    public const string Ppm = "PPM";
    public const string Security = "Security";
    public const string Identity = "Identity";
    public const string Messaging = "Messaging";
    public const string Organization = "Organization";
    public const string Planning = "Planning";
    public const string Work = "Work";
    public const string StrategicManagement = "StrategicManagement";
    public const string Integrations = "Integrations";
}
