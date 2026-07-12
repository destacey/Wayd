namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// Curated, realistic vocabulary for naming teams and roles. Purpose-built to replace the nonsense
/// Bogus word-salad ("Soft Corporate Rustic Soft Car") with names that read like a real engineering
/// organization: a product/business domain paired with a functional area.
/// </summary>
internal static class OrgVocabulary
{
    /// <summary>Business/product domains — the "what" a team owns.</summary>
    internal static readonly string[] Domains =
    [
        "Payments", "Checkout", "Identity", "Billing", "Catalog", "Search", "Fulfillment",
        "Onboarding", "Notifications", "Reporting", "Analytics", "Messaging", "Accounts",
        "Pricing", "Inventory", "Subscriptions", "Marketplace", "Loyalty", "Content",
        "Integrations", "Data Platform", "Developer Experience", "Growth", "Trust & Safety",
    ];

    /// <summary>Functional suffixes — the "kind" of team.</summary>
    internal static readonly string[] Functions =
    [
        "Platform", "Core", "Experience", "Services", "Engineering", "Infrastructure",
        "Enablement", "Foundations", "Squad", "Team",
    ];

    /// <summary>Suffix for an ART (the mid tier — a team of teams grouping delivery teams).</summary>
    internal static readonly string[] ArtSuffixes =
    [
        "ART", "Train", "Group", "Tribe",
    ];

    /// <summary>Suffix for a value stream (the top tier — a team of teams grouping ARTs).</summary>
    internal static readonly string[] ValueStreamSuffixes =
    [
        "Value Stream", "Portfolio", "Division",
    ];

    // Roles used at each tier of the hierarchy. Kept distinct from the discipline pool so tier leadership
    // reads clearly. All of these are ensured as TeamMemberRoles before staffing.

    /// <summary>Role for an engineering manager who leads a single team and is also an IC on it.</summary>
    internal const string EngineeringManagerRole = "Engineering Manager";

    /// <summary>Role for a product manager acting as product owner on a single team.</summary>
    internal const string ProductOwnerRole = "Product Owner";

    /// <summary>Role for the engineering leader over an ART (manages multiple teams' EMs).</summary>
    internal const string ArtEngineeringLeadRole = "RTE";

    /// <summary>Role for the product leader over an ART.</summary>
    internal const string ArtProductLeadRole = "Product Manager";

    /// <summary>Roles for the engineering and product leaders over a value stream.</summary>
    internal const string ValueStreamEngineeringLeadRole = "VP of Engineering";
    internal const string ValueStreamProductLeadRole = "VP of Product";

    /// <summary>
    /// Team-member roles (disciplines). The first entry is treated as a team's primary discipline pool
    /// anchor; staffing mixes these to create cross-functional teams.
    /// </summary>
    internal static readonly string[] Roles =
    [
        "Software Engineer", "Senior Software Engineer", "Staff Engineer", "Engineering Manager",
        "Product Manager", "Product Designer", "UX Researcher", "QA Engineer",
        "Data Engineer", "Data Analyst", "Site Reliability Engineer", "Technical Program Manager",
        "Scrum Master", "Business Analyst",
    ];

    /// <summary>Executive / management job titles, from the top of the tree down.</summary>
    internal static readonly string[] LeadershipTitles =
    [
        "Chief Executive Officer", "VP of Engineering", "VP of Product", "Director of Engineering",
        "Senior Engineering Manager", "Engineering Manager",
    ];

    /// <summary>Individual-contributor job titles for the leaf employees.</summary>
    internal static readonly string[] IndividualTitles =
    [
        "Software Engineer", "Senior Software Engineer", "Staff Engineer", "Product Manager",
        "Product Designer", "QA Engineer", "Data Engineer", "Site Reliability Engineer",
        "Technical Program Manager", "Business Analyst",
    ];

    internal static readonly string[] Departments =
    [
        "Engineering", "Product", "Design", "Data", "Platform", "Quality", "Operations",
    ];

    // ---- Executive layer ----------------------------------------------------------------------

    /// <summary>The regular full-time worker type. The vast majority of employees are this, and every
    /// manager is this.</summary>
    internal const string RegularEmployeeType = "Employee";

    /// <summary>
    /// Non-regular worker types, only assigned to a minority of non-manager individual contributors. Mirrors
    /// the free-form HRIS worker-type descriptor (Workday Worker_Type, Entra employeeType).
    /// </summary>
    internal static readonly string[] NonRegularEmployeeTypes =
    [
        "Contractor", "Contingent Worker", "Intern", "Part-Time",
    ];

    internal const string ChiefExecutiveTitle = "Chief Executive Officer";
    internal const string ChiefTechnologyTitle = "Chief Technology Officer";
    internal const string ChiefProductTitle = "Chief Product Officer";

    // ---- Non-delivery organization ------------------------------------------------------------

    /// <summary>
    /// A department outside the product-delivery team structure (Sales, Support, HR, …). Its people are
    /// real employees with a shallow management chain (head → managers → ICs) but hold NO team memberships,
    /// because they are not part of the cross-functional delivery org.
    /// </summary>
    internal sealed record NonDeliveryFunction(
        string Department,
        string HeadTitle,
        string ManagerTitle,
        string[] IndividualTitles);

    /// <summary>The non-delivery functions that make up "the rest of the company". Weight = relative size.</summary>
    internal static readonly (NonDeliveryFunction Function, double Weight)[] NonDeliveryFunctions =
    [
        (new NonDeliveryFunction("Sales", "Chief Revenue Officer", "Sales Manager",
            ["Account Executive", "Sales Development Rep", "Solutions Engineer", "Account Manager"]), 3.0),
        (new NonDeliveryFunction("Customer Support", "VP of Customer Support", "Support Team Lead",
            ["Support Engineer", "Customer Support Specialist", "Technical Support Analyst"]), 3.0),
        (new NonDeliveryFunction("Customer Success", "VP of Customer Success", "Customer Success Manager",
            ["Customer Success Specialist", "Onboarding Specialist", "Renewals Manager"]), 1.5),
        (new NonDeliveryFunction("Marketing", "Chief Marketing Officer", "Marketing Manager",
            ["Content Strategist", "Growth Marketer", "Demand Generation Specialist", "Brand Designer"]), 1.5),
        (new NonDeliveryFunction("Finance", "Chief Financial Officer", "Finance Manager",
            ["Financial Analyst", "Accountant", "Payroll Specialist", "FP&A Analyst"]), 1.0),
        (new NonDeliveryFunction("People", "Chief People Officer", "HR Manager",
            ["Recruiter", "HR Business Partner", "People Operations Specialist"]), 1.0),
        (new NonDeliveryFunction("Legal", "General Counsel", "Legal Operations Manager",
            ["Corporate Counsel", "Contracts Specialist", "Compliance Analyst"]), 0.5),
        (new NonDeliveryFunction("IT Operations", "VP of IT", "IT Manager",
            ["Systems Administrator", "IT Support Specialist", "Network Engineer"]), 1.0),
    ];
}
