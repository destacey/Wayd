using Wayd.Common.Domain.Authorization;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>One application role the seed creates, and the permissions it carries.</summary>
public sealed record SeedRole(string Name, string Description, IReadOnlyList<string> Permissions);

/// <summary>
/// The roles a seeded environment gets, chosen so that signing in as different people actually behaves
/// differently.
/// </summary>
/// <remarks>
/// <b>Admin is the only role Wayd ships</b>, and it carries
/// <c>Administer ProjectPortfolioManagement</c>, which waives the per-record delivery-leadership
/// requirement across the whole domain. Seeding every account as Admin would therefore switch off the
/// exact rules a seeded environment exists to exercise — so the seed creates a spread of narrower roles
/// and hands Admin to almost nobody.
/// <para>
/// Permissions are looked up from <see cref="ApplicationPermissions"/> rather than written as strings.
/// <c>UpdatePermissions</c> stores whatever it is sent without checking it against the catalogue, so a
/// renamed resource would be written as a claim that silently grants nothing. Reading them from the
/// catalogue makes that a compile error instead.
/// </para>
/// </remarks>
public static class SeedRoleCatalog
{
    public const string ReadOnly = "Read Only";
    public const string TeamMember = "Team Member";
    public const string ProjectManager = "Project Manager";
    public const string DeliveryManager = "Delivery Manager";
    public const string ProductManager = "Product Manager";
    public const string PpmAdministrator = "PPM Administrator";

    /// <summary>Every role the seed creates, narrowest first.</summary>
    public static IReadOnlyList<SeedRole> All { get; } =
    [
        new(ReadOnly,
            "Can see everything and change nothing.",
            [.. ViewEverything()]),

        new(TeamMember,
            "Read-only, plus the tasks and phases of any project. Work items gate on the permission alone, "
            + "with no leadership of the project required.",
            [.. ViewEverything().Append(Permission(ApplicationAction.ManageProjectWorkItems, ApplicationResource.Projects))]),

        new(ProjectManager,
            "Can run the projects they lead. The permission reaches every project; delivery leadership "
            + "decides which ones actually change.",
            [.. ProjectManagerPermissions()]),

        new(DeliveryManager,
            "A project manager who also holds the portfolios and programs above them.",
            [.. DeliveryManagerPermissions()]),

        new(ProductManager,
            "Runs the product catalogue. Product management has no Administer counterpart — the permission "
            + "alone is enough, with no membership involved.",
            [.. ProductManagerPermissions()]),

        new(PpmAdministrator,
            "A delivery manager who may change any PPM record regardless of who leads it. This is the "
            + "membership bypass, so it belongs to very few people.",
            [.. DeliveryManagerPermissions()
                .Append(Permission(ApplicationAction.Administer, ApplicationResource.ProjectPortfolioManagement))]),
    ];

    private static string Permission(string action, string resource) =>
        ApplicationPermission.NameFor(action, resource);

    /// <summary>
    /// Every read permission outside identity administration.
    /// </summary>
    /// <remarks>
    /// Derived from the catalogue so a new resource is readable by these roles the day it is added. The
    /// identity category is held back deliberately: who may see the user and role lists is an
    /// administrative question, not part of reading the delivery data.
    /// </remarks>
    private static IEnumerable<string> ViewEverything() =>
        ApplicationPermissions.Admin
            .Where(p => p.Action == ApplicationAction.View && p.Category != IdentityCategory)
            .Select(p => p.Name);

    private static IEnumerable<string> ProjectManagerPermissions() =>
        ViewEverything()
            .Append(Permission(ApplicationAction.ManageProjectWorkItems, ApplicationResource.Projects))
            .Append(Permission(ApplicationAction.Create, ApplicationResource.Projects))
            .Append(Permission(ApplicationAction.Update, ApplicationResource.Projects));

    private static IEnumerable<string> DeliveryManagerPermissions() =>
        ProjectManagerPermissions()
            .Append(Permission(ApplicationAction.Create, ApplicationResource.Programs))
            .Append(Permission(ApplicationAction.Update, ApplicationResource.Programs))
            .Append(Permission(ApplicationAction.Update, ApplicationResource.ProjectPortfolios));

    private static IEnumerable<string> ProductManagerPermissions() =>
        ViewEverything()
            .Append(Permission(ApplicationAction.Create, ApplicationResource.Products))
            .Append(Permission(ApplicationAction.Update, ApplicationResource.Products))
            .Append(Permission(ApplicationAction.Create, ApplicationResource.Releases))
            .Append(Permission(ApplicationAction.Update, ApplicationResource.Releases))
            .Append(Permission(ApplicationAction.Create, ApplicationResource.Delivery))
            .Append(Permission(ApplicationAction.Update, ApplicationResource.Delivery));

    /// <summary>
    /// The category the shipped permissions file gives identity administration, matched by value because
    /// it is a private constant there.
    /// </summary>
    private const string IdentityCategory = "Identity";
}
