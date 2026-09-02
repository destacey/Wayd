using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.ProductManagement.Domain;

/// <summary>
/// The kinds of Product Management record the status workflow engine governs, and what each one needs
/// from a workflow before it can be activated.
/// </summary>
/// <remarks>
/// Declared here rather than in the engine so Common carries no Product Management vocabulary.
/// <para>
/// The keys are persisted on every workflow row and on every status transition, and the alias values
/// on every record that holds a status. <strong>Never change either once shipped</strong> — renaming a
/// key orphans its workflows, and renumbering an alias silently changes what existing rows mean.
/// </para>
/// <para>
/// Delivery keys are namespaced <c>delivery.*</c> rather than <c>product.*</c>: versions, releases,
/// packages and deployments live in the Delivery schema and may become their own module, while the
/// catalog stays Product Management. Only <c>product.product</c> is catalog.
/// </para>
/// </remarks>
public static class ProductWorkflowOwners
{
    /// <summary>A node in the product taxonomy. Governs the product lifecycle.</summary>
    public static readonly WorkflowOwnerDescriptor Product = new(
        "product.product",
        "Product",
        Names(ProductStatusAlias.Active, ProductStatusAlias.Sunset, ProductStatusAlias.Retired),
        [(int)ProductStatusAlias.Active, (int)ProductStatusAlias.Retired]);

    /// <summary>A versioned cut of a releasable product node.</summary>
    /// <remarks>
    /// The engineering record shipped under the key <c>delivery.release</c>, because it was called
    /// Release before the announcement record existed. Its workflows and status transitions were moved
    /// onto this key by migration, since the history belongs to the record that made it — leaving them
    /// behind would have attributed a version's cut-and-ship history to an announcement that never
    /// happened.
    /// </remarks>
    public static readonly WorkflowOwnerDescriptor Version = new(
        "delivery.version",
        "Version",
        Names(ProductStatusAlias.Ready, ProductStatusAlias.Released, ProductStatusAlias.Withdrawn),
        [(int)ProductStatusAlias.Released, (int)ProductStatusAlias.Withdrawn]);

    /// <summary>What was announced to customers, gathering the versions and packages that carried it.</summary>
    /// <remarks>
    /// Shares the version lifecycle vocabulary but not its meaning: a release is announced and
    /// retracted where a version is cut and pulled. <c>Ready</c> is required for the same reason it is
    /// on a version — the workflow's non-terminal resting state — even though nothing cuts a release.
    /// </remarks>
    public static readonly WorkflowOwnerDescriptor Release = new(
        "delivery.release",
        "Release",
        Names(ProductStatusAlias.Ready, ProductStatusAlias.Released, ProductStatusAlias.Withdrawn),
        [(int)ProductStatusAlias.Released, (int)ProductStatusAlias.Withdrawn]);

    /// <summary>A coordinated shipment of several component releases.</summary>
    public static readonly WorkflowOwnerDescriptor ReleasePackage = new(
        "delivery.release-package",
        "Release Package",
        Names(ProductStatusAlias.Ready, ProductStatusAlias.Released, ProductStatusAlias.Withdrawn),
        [(int)ProductStatusAlias.Released, (int)ProductStatusAlias.Withdrawn]);

    /// <summary>
    /// One release or package reaching one environment. Governs the deployment outcome.
    /// </summary>
    /// <remarks>
    /// The strictest of the four: change failure rate is <c>(Failed + RolledBack) / total</c> and
    /// time-to-restore measures from a failure to the next success, so an organization that could omit
    /// those outcomes would make both uncomputable and non-comparable between organizations.
    /// <para>
    /// <c>InProgress</c> is required for a different reason: starting a deployment resolves it, so a
    /// published workflow without it fails every deployment start in its scope. Requiring it here is
    /// what moves that failure from runtime to publish time.
    /// </para>
    /// </remarks>
    public static readonly WorkflowOwnerDescriptor Deployment = new(
        "delivery.deployment",
        "Deployment",
        Names(ProductStatusAlias.InProgress, ProductStatusAlias.Succeeded, ProductStatusAlias.Failed, ProductStatusAlias.RolledBack),
        [
            (int)ProductStatusAlias.InProgress,
            (int)ProductStatusAlias.Succeeded,
            (int)ProductStatusAlias.Failed,
            (int)ProductStatusAlias.RolledBack,
        ]);

    /// <summary>
    /// Every owner type this module contributes, for registration at startup.
    /// </summary>
    public static WorkflowOwnerDescriptor[] All => [Product, Version, Release, ReleasePackage, Deployment];

    /// <summary>
    /// Registers this module's owner types with the engine. Call once during startup, before anything
    /// resolves a workflow.
    /// </summary>
    public static void Register() => WorkflowOwners.Register(All);

    private static Dictionary<int, string> Names(params ProductStatusAlias[] aliases) =>
        aliases.ToDictionary(a => (int)a, a => a.ToString());
}
