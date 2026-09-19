namespace Wayd.Tests.Shared.Traits;

/// <summary>
/// Trait NAMES for the independent axes a test can be selected on. Kept as constants so a filter and the
/// attribute that satisfies it cannot drift apart — a typo in one branch of a combined filter matches
/// nothing, so the run silently covers fewer tests rather than erroring.
/// </summary>
public static class TestTraits
{
    /// <summary>What kind of test it is. Applied per-assembly, not per-class — see below.</summary>
    public const string Category = "Category";

    /// <summary>Infrastructure the test cannot run without. Applied per-assembly, not per-class — see below.</summary>
    public const string Requires = "Requires";

    /// <summary>When the test runs: a curated fast pass versus the full body of tests.</summary>
    public const string Suite = "Suite";

    /// <summary>What the test covers, cutting across projects.</summary>
    public const string Area = "Area";
}

/// <summary>
/// Values for <see cref="TestTraits.Category"/>.
/// <para>
/// These are applied at ASSEMBLY level from Directory.Build.targets: a <c>*.IntegrationTests</c> project (or
/// one referencing Testcontainers) is Integration, everything else Unit. Do not apply them by hand to a
/// class: a per-class copy is a second source of truth that drifts from the project, which is exactly how
/// the previous per-class "Docker" trait ended up on six classes in one of the four Docker-dependent projects.
/// </para>
/// </summary>
public static class TestCategories
{
    /// <summary>No external dependency: runs anywhere, needs no Docker daemon or network.</summary>
    public const string Unit = "Unit";

    /// <summary>Runs against a real backing service: a Testcontainers SQL Server, or a live external system.</summary>
    public const string Integration = "Integration";
}

/// <summary>
/// Values for <see cref="TestTraits.Requires"/>, applied at ASSEMBLY level from Directory.Build.targets. An
/// assembly without the trait needs nothing beyond the .NET runtime.
/// </summary>
public static class TestRequirements
{
    /// <summary>References Testcontainers, so it needs a Docker daemon. CI's integration job runs exactly these.</summary>
    public const string Docker = "Docker";
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
