namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The words the Request Desk generator draws from: the kinds of ask a company receives, the channels Wayd
/// seeds, the default review workflow's statuses, and the tag axes a desk files by.
/// </summary>
internal static class DemandManagementVocabulary
{
    // ---- Seeded channels --------------------------------------------------------------------
    // Wayd seeds these on a new environment; a request names one, and the seed resolves it by name.

    public const string BusinessStakeholder = "Business stakeholder";
    public const string CustomerFeedback = "Customer feedback";
    public const string Support = "Support";
    public const string Sales = "Sales and account management";
    public const string Innovation = "Innovation program";
    public const string Regulatory = "Regulatory and compliance";
    public const string Security = "Security";
    public const string Engineering = "Engineering";

    // ---- The default workflow's statuses -----------------------------------------------------

    public const string Submitted = "Submitted";
    public const string InReview = "In Review";
    public const string WaitingOnRequester = "Waiting on Requester";
    public const string Accepted = "Accepted";
    public const string Fulfilled = "Fulfilled";
    public const string Declined = "Declined";
    public const string Withdrawn = "Withdrawn";

    /// <summary>Which desk a kind of ask goes to.</summary>
    public enum DeskKind
    {
        /// <summary>The desk of the value stream whose products the ask is about.</summary>
        Product,

        /// <summary>The IT desk: internal systems and tooling.</summary>
        It,

        /// <summary>The Security and Compliance desk.</summary>
        SecurityAndCompliance,
    }

    /// <summary>
    /// One kind of ask: the channel it comes through, the departments that raise it, the desk it goes to, and
    /// how it reads. In titles and descriptions <c>{0}</c> is a value stream's domain and <c>{1}</c> a filler
    /// the kind supplies.
    /// </summary>
    public sealed record RequestKind(
        string Channel,
        string[] Departments,
        DeskKind Desk,
        double Weight,
        string[] Titles,
        string[] Fillers,
        string[] Descriptions,
        string[] Benefits);

    public static readonly RequestKind[] Kinds =
    [
        new(CustomerFeedback, ["Customer Success", "Product", "Marketing"], DeskKind.Product, 2.0,
            [
                "Let customers export their {0} history to CSV",
                "Add bulk editing to {0}",
                "Show {1} in the {0} dashboard",
                "Let customers save filtered views in {0}",
                "Notify customers when {1} changes in {0}",
            ],
            ["usage trends", "billing status", "renewal dates", "open issues", "team activity"],
            [
                "Several customers have asked for this in quarterly reviews. Today they work around it by copying data out of {0} by hand.",
                "This came up in the last customer advisory board: it is the most requested change to {0} this year.",
                "Customers keep raising this in onboarding calls. Without it, {1} has to be pieced together from three screens.",
            ],
            [
                "Fewer manual workarounds for customers, and fewer escalations to their success manager.",
                "Better retention among accounts that rely on {0} day to day.",
            ]),
        new(Support, ["Customer Support"], DeskKind.Product, 2.0,
            [
                "Reduce tickets about {1} in {0}",
                "Fix confusing error messages in {0}",
                "Give support a read-only view of {0} accounts",
                "Stop {0} sessions timing out during long forms",
            ],
            ["password resets", "failed imports", "duplicate invoices", "missing notifications", "permission errors"],
            [
                "Support handles a steady stream of tickets about {1}. Most are resolved with the same answer, which the product could give instead.",
                "Agents spend time reproducing what the customer sees in {0}. A view of the account would cut handling time.",
            ],
            [
                "Lower ticket volume and shorter handling time for the support team.",
                "Customers get unstuck without waiting for an agent.",
            ]),
        new(Sales, ["Sales"], DeskKind.Product, 1.5,
            [
                "Single sign-on for {0} needed to close {1}",
                "Regional data residency for {0} for {1}",
                "Audit log export in {0} requested by {1}",
                "Custom roles in {0} for {1}",
            ],
            ["Northwind Traders", "Contoso", "Fabrikam", "Adventure Works", "Tailspin Toys", "Wide World Importers"],
            [
                "{1} has made this a condition of signing. The deal is in the late stage of the pipeline.",
                "{1} raised this during security review. Two other prospects in the same segment have asked for it.",
            ],
            [
                "Unblocks a late-stage deal and opens the enterprise segment.",
                "Keeps a renewing account that has said it will otherwise evaluate competitors.",
            ]),
        new(BusinessStakeholder, ["Finance", "People", "Marketing", "Sales"], DeskKind.It, 1.5,
            [
                "Automate the monthly {1}",
                "Connect the HR system to payroll",
                "Replace the {1} spreadsheet with a shared tool",
                "Give managers a self-service report of {1}",
            ],
            ["expense reconciliation", "headcount report", "commission calculation", "campaign budget tracking", "contractor onboarding"],
            [
                "The {1} is done by hand every month and takes the team about two days. Errors are found late.",
                "Several teams keep their own copy of the {1}, and the numbers rarely agree.",
            ],
            [
                "Saves the team two days a month and removes a source of reporting errors.",
                "One version of the numbers that every team can rely on.",
            ]),
        new(Engineering, ["Engineering", "Platform", "Data", "Quality", "Operations"], DeskKind.Product, 1.5,
            [
                "Retire the legacy {1} behind {0}",
                "Upgrade {0} to a supported runtime",
                "Add automated tests around {0} {1}",
                "Split the {0} {1} into its own service",
            ],
            ["batch jobs", "reporting database", "message queue", "authentication module", "file storage"],
            [
                "The {1} behind {0} is the source of most of its incidents and slows every change that touches it.",
                "The runtime under {0} leaves support next year. Staying on it means no security patches.",
            ],
            [
                "Fewer incidents and faster delivery on {0}.",
                "Removes an end-of-support risk before it lands.",
            ]),
        new(Innovation, ["Product", "Engineering", "Design", "Marketing", "Customer Success"], DeskKind.Product, 1.0,
            [
                "Prototype {1} for {0}",
                "Explore {1} in {0}",
            ],
            ["AI-assisted search", "a mobile companion app", "usage-based pricing", "in-product guided tours", "a public API"],
            [
                "This came out of the last innovation week. The prototype drew a lot of interest and needs a decision on whether to take it further.",
                "A small experiment could tell us whether {1} is worth building into {0}.",
            ],
            [
                "A tested answer on whether {1} is worth investing in.",
            ]),
        new(Regulatory, ["Legal", "Finance"], DeskKind.SecurityAndCompliance, 1.0,
            [
                "Meet {1} requirements for {0}",
                "Record consent for {0} under {1}",
                "Data retention schedule for {0} under {1}",
            ],
            ["GDPR", "SOC 2", "PCI DSS 4.0", "the EU Accessibility Act", "CCPA"],
            [
                "Legal has reviewed {0} against {1} and found gaps that must be closed before the deadline.",
                "Our {1} audit flagged {0}. The finding has to be remediated before the next assessment.",
            ],
            [
                "Keeps the company compliant and avoids fines or a failed audit.",
            ]),
        new(Security, ["IT Operations", "Platform", "Engineering"], DeskKind.SecurityAndCompliance, 1.0,
            [
                "Remediate {1} in {0}",
                "Enforce multi-factor sign-in for {0} administrators",
                "Rotate {0} service credentials automatically",
            ],
            ["outdated TLS settings", "an exposed admin endpoint", "excessive service permissions", "unencrypted backups"],
            [
                "The last penetration test reported {1} in {0}. It is rated high and has an agreed remediation date.",
                "A security review found {1} in {0}. It has not been exploited, but it widens what an attacker could reach.",
            ],
            [
                "Closes a high-rated finding and reduces exposure.",
            ]),
    ];

    // ---- Need-by references -----------------------------------------------------------------

    public static readonly string[] RegulatoryReferences =
    [
        "{0}, enforcement date for the new requirements",
        "{0}: remediation deadline agreed with the auditor",
    ];

    public static readonly string[] ContractualReferences =
    [
        "Order form with {0}, clause 4.2",
        "Master services agreement with {0}, schedule B",
    ];

    public static readonly string[] ExternalEventReferences =
    [
        "End of support for the current runtime",
        "Customer go-live date",
        "Annual conference launch",
    ];

    public static readonly string[] CommitmentReferences =
    [
        "Committed at the quarterly business review",
        "Promised to the leadership team in the planning offsite",
    ];

    public static readonly string[] PreferenceReferences =
    [
        "Wanted before the next planning cycle",
        "Would like it before the busy season",
    ];

    // ---- Status reasons ---------------------------------------------------------------------

    public static readonly string[] DeclineReasons =
    [
        "Not a priority this year against the rest of the roadmap.",
        "Already covered by work in flight; we will update you when it ships.",
        "The cost outweighs the benefit for the number of customers affected.",
        "Better solved with a process change than with new software.",
    ];

    public static readonly string[] FulfilledReasons =
    [
        "Delivered and released to all customers.",
        "Shipped in the latest release.",
        "Done as part of the team's regular backlog.",
    ];

    public static readonly string[] WithdrawnNotes =
    [
        "No longer needed after the reorganization.",
        "We found a workaround that is good enough.",
    ];

    // ---- Tag axes ---------------------------------------------------------------------------

    public static readonly IntakeTagCategoryModel Size = new()
    {
        Name = "Size",
        Description = "A first estimate of how big the ask is.",
        AllowsMany = false,
        Tags = ["Small", "Medium", "Large", "Extra Large"],
    };

    public static readonly IntakeTagCategoryModel Theme = new()
    {
        Name = "Theme",
        Description = "What the ask is trying to achieve.",
        AllowsMany = true,
        Tags = ["Revenue Growth", "Customer Experience", "Cost Reduction", "Risk Reduction", "Automation"],
    };

    // ---- Origins ----------------------------------------------------------------------------

    public const string ServiceDesk = "ServiceNow";
    public const string ServiceDeskUrl = "https://servicenow.example.com/request/";
}
