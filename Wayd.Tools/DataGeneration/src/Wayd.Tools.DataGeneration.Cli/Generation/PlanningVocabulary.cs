namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The values and names the Planning generator draws on. Statuses, categories and grades are Wayd's own
/// enums, which the imports take by numeric id, so the model carries the name and the id lives here beside it.
/// </summary>
internal static class PlanningVocabulary
{
    // ---- Reference data Wayd defines ------------------------------------------------------------

    internal const string NotStarted = "Not Started";
    internal const string InProgress = "In Progress";
    internal const string Completed = "Completed";
    internal const string Canceled = "Canceled";
    internal const string Missed = "Missed";

    /// <summary>Wayd's <c>ObjectiveStatus</c> values.</summary>
    internal static readonly IReadOnlyDictionary<string, int> ObjectiveStatusIds = new Dictionary<string, int>
    {
        [NotStarted] = 1,
        [InProgress] = 2,
        [Completed] = 3,
        [Canceled] = 4,
        [Missed] = 5,
    };

    /// <summary>The statuses an objective carries a closed date in; the import requires it for exactly these.</summary>
    internal static bool IsClosed(string objectiveStatus) => objectiveStatus is Completed or Canceled or Missed;

    internal const string Open = "Open";
    internal const string Closed = "Closed";

    /// <summary>Wayd's <c>RiskStatus</c> values.</summary>
    internal static readonly IReadOnlyDictionary<string, int> RiskStatusIds = new Dictionary<string, int>
    {
        [Open] = 1,
        [Closed] = 2,
    };

    internal const string Resolved = "Resolved";
    internal const string Owned = "Owned";
    internal const string Accepted = "Accepted";
    internal const string Mitigated = "Mitigated";

    /// <summary>Wayd's <c>RiskCategory</c> values — the ROAM categories.</summary>
    internal static readonly IReadOnlyDictionary<string, int> RiskCategoryIds = new Dictionary<string, int>
    {
        [Resolved] = 1,
        [Owned] = 2,
        [Accepted] = 3,
        [Mitigated] = 4,
    };

    internal const string Low = "Low";
    internal const string Medium = "Medium";
    internal const string High = "High";

    /// <summary>Wayd's <c>RiskGrade</c> values, which both impact and likelihood take.</summary>
    internal static readonly IReadOnlyDictionary<string, int> RiskGradeIds = new Dictionary<string, int>
    {
        [Low] = 1,
        [Medium] = 2,
        [High] = 3,
    };

    // ---- Objectives ----------------------------------------------------------------------------

    /// <summary>What a team delivers into one of its own components. <c>{0}</c> is the component, <c>{1}</c> a feature.</summary>
    internal static readonly string[] ComponentObjectives =
    [
        "Deliver {1} in {0}",
        "Launch {1} for {0} customers",
        "Enable {1} in {0} behind a feature flag",
        "Complete {1} for {0}",
    ];

    /// <summary>Work on a component rather than a feature. <c>{0}</c> is the component, <c>{1}</c> a quality or platform.</summary>
    internal static readonly string[] ComponentImprovements =
    [
        "Improve {1} in {0}",
        "Reduce {1} for {0}",
        "Migrate {0} to {1}",
    ];

    /// <summary>Objectives a team without a component of its own still plans. <c>{0}</c> is a feature.</summary>
    internal static readonly string[] TeamObjectives =
    [
        "Deliver {0}",
        "Prototype {0} with two pilot customers",
        "Define and validate {0}",
    ];

    internal static readonly string[] Features =
    [
        "self-service refunds", "bulk export", "saved searches", "single sign-on onboarding",
        "usage-based billing", "audit history", "offline mode", "role-based access",
        "in-app notifications", "partner API v2", "multi-currency pricing", "guided setup",
        "scheduled reports", "account merging", "data residency controls", "custom dashboards",
    ];

    internal static readonly string[] Improvements =
    [
        "p95 latency", "error rates", "cold-start time", "test coverage", "on-call load",
        "deployment time", "infrastructure cost",
    ];

    internal static readonly string[] Platforms =
    [
        "the shared event bus", "managed Kubernetes", "the new identity provider",
        "the observability stack", "the regional data platform",
    ];

    internal static readonly string[] ObjectiveMeasures =
    [
        "Measured by adoption among active accounts.",
        "Done when it is live for every customer and monitored.",
        "Measured against the baseline taken at planning.",
        "Accepted by the product owner at the system demo.",
        "Done when support has signed off the runbook.",
    ];

    // ---- Risks ---------------------------------------------------------------------------------

    /// <summary><c>{0}</c> is another team on the same train.</summary>
    internal static readonly string[] DependencyRisks =
    [
        "Dependency on {0} may slip past our integration date",
        "{0} has not committed capacity for the shared API change",
        "Contract changes from {0} are not yet versioned",
    ];

    /// <summary><c>{0}</c> is one of the team's components.</summary>
    internal static readonly string[] ComponentRisks =
    [
        "Third-party SDK deprecation affects {0}",
        "Compliance review for {0} is not yet scheduled",
        "Data migration for {0} is larger than estimated",
        "Load testing for {0} needs more environment capacity",
    ];

    internal static readonly string[] GeneralRisks =
    [
        "Key engineer on leave during two iterations",
        "Acceptance criteria for the top feature are still unclear",
        "Vendor contract renewal is pending legal review",
        "Test environment is shared and frequently unavailable",
        "Security sign-off needed before release has no date",
        "Estimate for the largest feature has low confidence",
    ];

    internal static readonly string[] RiskDescriptions =
    [
        "Raised at planning and reviewed at each ART sync.",
        "Could push committed objectives into the next interval if it lands.",
        "Flagged during the iteration review.",
        "Would block the release train's system demo if unresolved.",
    ];

    internal static readonly Dictionary<string, string[]> RiskResponses = new()
    {
        [Resolved] =
        [
            "Resolved: the dependency landed ahead of schedule.",
            "No longer a threat after the scope was clarified.",
        ],
        [Owned] =
        [
            "Owner is following up with the stakeholders this week.",
            "Owner is tracking it at the ART sync.",
        ],
        [Accepted] =
        [
            "Accepted: the impact is tolerable for this interval.",
            "Accepted and carried as a known constraint.",
        ],
        [Mitigated] =
        [
            "Mitigated by adding a fallback path.",
            "Mitigated by rescheduling the work into a later iteration.",
            "Mitigated by pairing a second engineer on the work.",
        ],
    };
}
