namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Curated, realistic vocabulary for naming PPM entities — portfolios, programs, projects, themes, KPIs,
/// lifecycles and expenditure categories. Like <see cref="OrgVocabulary"/> for the org side, this replaces
/// Bogus word-salad with names that read like a real portfolio-management organization.
/// </summary>
public static class PpmVocabulary
{
    /// <summary>
    /// The house style a company names its function portfolios in. One style is chosen per generated
    /// company so its portfolios read coherently (a bank does not mix "Consumer Banking" with "Developer
    /// Experience"). The value-stream portfolios keep their org domain name regardless of style.
    /// </summary>
    public enum PortfolioStyle
    {
        BusinessFunction,
        BusinessUnit,
        Strategic,
        Technology,
        InvestmentBased,
        ProductBased,
    }

    /// <summary>The names available in each portfolio style. A generated company draws its function portfolios from one list.</summary>
    public static readonly IReadOnlyDictionary<PortfolioStyle, string[]> PortfolioNamesByStyle =
        new Dictionary<PortfolioStyle, string[]>
        {
            [PortfolioStyle.BusinessFunction] =
            [
                "Information Technology", "Digital Transformation", "Infrastructure", "Cybersecurity",
                "Finance", "Human Resources", "Sales", "Marketing", "Customer Experience", "Operations",
                "Supply Chain", "Manufacturing", "Research & Development", "Product Development",
                "Legal & Compliance",
            ],
            [PortfolioStyle.BusinessUnit] =
            [
                "Consumer Banking", "Commercial Banking", "Wealth Management", "Retail",
                "Enterprise Solutions", "Healthcare Services", "Insurance", "Cloud Services",
                "Government Solutions", "North America", "Europe", "Asia-Pacific",
            ],
            [PortfolioStyle.Strategic] =
            [
                "Innovation", "Artificial Intelligence", "Digital Modernization", "Customer Growth",
                "Market Expansion", "Sustainability", "Operational Excellence", "Business Transformation",
                "Data & Analytics", "Platform Modernization",
            ],
            [PortfolioStyle.Technology] =
            [
                "Enterprise Applications", "Data Platform", "Infrastructure", "Networking",
                "Identity & Access Management", "Cloud Migration", "Developer Experience", "Internal Tools",
                "Business Intelligence", "Enterprise Architecture",
            ],
            [PortfolioStyle.InvestmentBased] =
            [
                "Run the Business", "Grow the Business", "Transform the Business", "Compliance",
                "Risk Reduction", "Cost Optimization", "Revenue Growth", "Innovation",
                "Technical Debt Reduction", "Capital Projects",
            ],
            [PortfolioStyle.ProductBased] =
            [
                "Mobile Applications", "Web Platform", "Payment Solutions", "Healthcare Products",
                "Analytics Suite", "Security Products", "Consumer Products", "Enterprise Platform",
                "IoT Solutions", "Developer Platform",
            ],
        };

    /// <summary>Suffixes appended to a value stream's domain to name its portfolio (e.g. "Payments Portfolio").</summary>
    public static readonly string[] ValueStreamPortfolioSuffixes =
    [
        "Portfolio", "Delivery", "Programs",
    ];

    /// <summary>
    /// The thematic programs a portfolio groups its projects into. A program is an investment-level bucket of
    /// related work (not a delivery unit), so a portfolio runs several at once and a project is sorted into one
    /// by what kind of work it is. <see cref="ProgramTheme.ProjectVerbs"/> lists the project verbs that lean
    /// toward each theme, which is how a project picks its program.
    /// </summary>
    public static readonly ProgramTheme[] ProgramThemes =
    [
        new("Modernization", "Replacing and upgrading legacy systems.",
            ["Migrate", "Modernize", "Rebuild", "Decommission", "Redesign"]),
        new("Integrations", "Connecting systems and partners end to end.",
            ["Integrate", "Consolidate", "Unify"]),
        new("Client-Driven", "Work requested or prioritized by customers.",
            ["Launch", "Roll out"]),
        new("Growth & Scale", "Expanding capacity, reach and performance.",
            ["Scale", "Optimize"]),
        new("Automation & AI", "Automating workflows and applying AI.",
            ["Automate"]),
        new("Platform & Reliability", "Hardening the platform and paying down debt.",
            ["Harden", "Streamline"]),
    ];

    /// <summary>Suffixes appended to a program theme to name it within its portfolio (e.g. "Payments Modernization Program").</summary>
    public static readonly string[] ProgramNameSuffixes =
    [
        "Program", "Initiative", "",
    ];

    /// <summary>Verb-ish leading words for a project name (e.g. "Migrate Billing to the new ledger").</summary>
    public static readonly string[] ProjectVerbs =
    [
        "Migrate", "Modernize", "Rebuild", "Launch", "Scale", "Automate", "Consolidate", "Integrate",
        "Redesign", "Optimize", "Roll out", "Decommission", "Harden", "Streamline", "Unify",
    ];

    /// <summary>Objects a project acts on, paired with a verb (e.g. "Automate the onboarding flow").</summary>
    public static readonly string[] ProjectObjects =
    [
        "the onboarding flow", "the billing platform", "the reporting pipeline", "the mobile app",
        "the checkout experience", "the data warehouse", "the identity service", "the legacy monolith",
        "the notification system", "the search index", "the customer portal", "the payments gateway",
        "the analytics dashboard", "the CI/CD pipeline", "the content platform", "the API gateway",
        "the fraud detection engine", "the subscription service", "the partner integrations",
        "the internal tooling",
    ];

    /// <summary>Strategic themes — cross-cutting priorities projects and programs align to.</summary>
    public static readonly string[] StrategicThemes =
    [
        "Customer Obsession", "Operational Efficiency", "Platform Reliability", "Data-Driven Decisions",
        "Security & Trust", "Developer Productivity", "Cost Discipline", "Speed to Market",
        "Sustainability", "Regulatory Compliance", "Mobile First", "AI & Automation",
    ];

    /// <summary>Expenditure categories, bootstrapped via the settings API and referenced by name.</summary>
    public static readonly ExpenditureCategoryDefinition[] ExpenditureCategories =
    [
        new("Capital Expenditure", "Investments in long-lived assets that are capitalized and depreciated.", IsCapitalizable: true, RequiresDepreciation: true, "CAPEX"),
        new("Operating Expenditure", "Ongoing operational costs expensed in the period they are incurred.", IsCapitalizable: false, RequiresDepreciation: false, "OPEX"),
        new("Research & Development", "Investment in new products and capabilities, capitalized where eligible.", IsCapitalizable: true, RequiresDepreciation: false, "RND"),
        new("Maintenance", "Keeping existing systems running; expensed operationally.", IsCapitalizable: false, RequiresDepreciation: false, "MAINT"),
    ];

    /// <summary>The project lifecycle to bootstrap and assign to projects, with its ordered phases.</summary>
    public static readonly ProjectLifecycleDefinition StandardLifecycle = new(
        "Standard Delivery",
        "A standard phased delivery lifecycle from initiation through closure.",
        [
            new("Initiation", "Define the problem, scope and business case."),
            new("Planning", "Break down the work, estimate and sequence it."),
            new("Execution", "Build and deliver the planned work."),
            new("Stabilization", "Harden, test and prepare for release."),
            new("Closure", "Release, hand over and capture learnings."),
        ]);

    /// <summary>KPI templates used to give strategic initiatives realistic measures.</summary>
    public static readonly KpiTemplate[] KpiTemplates =
    [
        new("Annual Recurring Revenue", null, TargetValue: 25_000_000, StartingValue: 12_000_000, Prefix: "$", Suffix: "M", "Increase"),
        new("Customer Satisfaction", "CSAT across the portfolio's products.", TargetValue: 90, StartingValue: 74, Prefix: null, Suffix: "%", "Increase"),
        new("Time to Market", "Median weeks from kickoff to launch.", TargetValue: 8, StartingValue: 16, Prefix: null, Suffix: " wks", "Decrease"),
        new("Operating Cost", "Run cost of the platform per year.", TargetValue: 4_000_000, StartingValue: 6_500_000, Prefix: "$", Suffix: "M", "Decrease"),
        new("Adoption Rate", "Share of eligible customers using the new capability.", TargetValue: 60, StartingValue: 10, Prefix: null, Suffix: "%", "Increase"),
        new("Incident Rate", "Sev-1 incidents per quarter.", TargetValue: 1, StartingValue: 5, Prefix: null, Suffix: null, "Decrease"),
    ];

    /// <summary>Names for strategic initiatives — a longer-horizon outcome a portfolio pursues.</summary>
    public static readonly string[] InitiativeNames =
    [
        "Grow enterprise revenue", "Reduce operating cost", "Improve customer retention",
        "Modernize the core platform", "Expand into new markets", "Strengthen security posture",
        "Accelerate delivery speed", "Deepen data & analytics", "Improve developer experience",
        "Achieve regulatory readiness",
    ];

    /// <summary>
    /// A thematic program: a name fragment (paired with the portfolio's name and a suffix) and the project
    /// verbs whose projects lean toward it.
    /// </summary>
    public sealed record ProgramTheme(string Name, string Description, IReadOnlyList<string> ProjectVerbs);

    public sealed record ExpenditureCategoryDefinition(
        string Name,
        string Description,
        bool IsCapitalizable,
        bool RequiresDepreciation,
        string AccountingCode);

    public sealed record ProjectLifecycleDefinition(
        string Name,
        string Description,
        IReadOnlyList<ProjectLifecyclePhaseDefinition> Phases);

    public sealed record ProjectLifecyclePhaseDefinition(string Name, string Description);

    public sealed record KpiTemplate(
        string Name,
        string? Description,
        double TargetValue,
        double StartingValue,
        string? Prefix,
        string? Suffix,
        string TargetDirection);
}
