using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

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
/// <para>
/// The four delivery owner types move only through named methods (cut, release, withdraw, fail), so
/// each declares the moves its methods make. A restricted delivery workflow must allow the required
/// ones — those the domain or the delivery figures depend on — and may leave out the optional ones,
/// which refuses the method that makes them. No delivery move accepts a condition: none of these
/// methods can be given one by the people, imports and deployments that call them.
/// </para>
/// <para>
/// Each owner type's required categories and alias categories match its seeded workflow, so every
/// seeded workflow satisfies them. A deployment requires no Proposed category, since it starts In
/// Progress, and a release package no Active one, since its Ready is optional.
/// </para>
/// </remarks>
public static class ProductWorkflowOwners
{
    /// <summary>
    /// A move's end when it is the workflow's initial status rather than an aliased one, named so the
    /// declarations below read as moves rather than as nulls.
    /// </summary>
    private static readonly int? Initial = null;

    /// <summary>
    /// The aliases versions, releases and release packages share: cut or ready to go (Active), shipped
    /// (Done), and pulled (Removed).
    /// </summary>
    private static readonly Dictionary<int, WorkflowAlias> DeliveryNames = Names(
        (ProductStatusAlias.Ready, StatusCategory.Active),
        (ProductStatusAlias.Released, StatusCategory.Done),
        (ProductStatusAlias.Withdrawn, StatusCategory.Removed));

    /// <summary>
    /// The categories versions and releases need: planned, ready, shipped and pulled.
    /// </summary>
    private static readonly StatusCategory[] DeliveryCategories =
        [StatusCategory.Proposed, StatusCategory.Active, StatusCategory.Done, StatusCategory.Removed];

    /// <summary>A node in the product taxonomy. Governs the product lifecycle.</summary>
    /// <remarks>
    /// Supports restricted transitions: a product changes status through one method that takes any
    /// target, so a custom transition set cannot contradict a domain rule, and it declares no moves.
    /// </remarks>
    public static readonly WorkflowOwnerDescriptor Product = new(
        "product.product",
        "Product",
        Names(
            (ProductStatusAlias.Active, StatusCategory.Active),
            (ProductStatusAlias.Sunset, StatusCategory.Active),
            (ProductStatusAlias.Retired, StatusCategory.Done)),
        [(int)ProductStatusAlias.Active, (int)ProductStatusAlias.Retired],
        [StatusCategory.Proposed, StatusCategory.Active, StatusCategory.Done],
        supportsRestrictedTransitions: true);

    /// <summary>A versioned cut of a releasable product node.</summary>
    /// <remarks>
    /// The engineering record shipped under the key <c>delivery.release</c>, because it was called
    /// Release before the announcement record existed. Its workflows and status transitions were moved
    /// onto this key by migration, since the history belongs to the record that made it — leaving them
    /// behind would have attributed a version's cut-and-ship history to an announcement that never
    /// happened.
    /// <para>
    /// <c>Ready</c> is required because cutting a version, reverting a cut version's release and
    /// importing versions all resolve it, so a published workflow without it would fail each of those
    /// at runtime. Requiring it here moves that failure to publish time.
    /// </para>
    /// <para>
    /// Releasing an uncut version from its initial status is required, not optional: a production
    /// deployment releases the version it shipped, and an import records a version that shipped without
    /// ever being cut. Reverting a release and withdrawing a released version are a company's choice.
    /// </para>
    /// </remarks>
    public static readonly WorkflowOwnerDescriptor Version = new(
        "delivery.version",
        "Version",
        DeliveryNames,
        [(int)ProductStatusAlias.Ready, (int)ProductStatusAlias.Released, (int)ProductStatusAlias.Withdrawn],
        DeliveryCategories,
        supportsRestrictedTransitions: true,
        moves:
        [
            WorkflowMove.Required("Cut", Initial, (int)ProductStatusAlias.Ready),
            WorkflowMove.Required("Mark Released", Initial, (int)ProductStatusAlias.Released),
            WorkflowMove.Required("Mark Released", (int)ProductStatusAlias.Ready, (int)ProductStatusAlias.Released),
            WorkflowMove.Optional("Revert Version", (int)ProductStatusAlias.Released, (int)ProductStatusAlias.Ready),
            WorkflowMove.Optional("Revert Version", (int)ProductStatusAlias.Released, Initial),
            WorkflowMove.Required("Withdraw", Initial, (int)ProductStatusAlias.Withdrawn),
            WorkflowMove.Required("Withdraw", (int)ProductStatusAlias.Ready, (int)ProductStatusAlias.Withdrawn),
            WorkflowMove.Optional("Withdraw", (int)ProductStatusAlias.Released, (int)ProductStatusAlias.Withdrawn),
        ]);

    /// <summary>What was announced to customers, gathering the versions and packages that carried it.</summary>
    /// <remarks>
    /// Shares the version lifecycle vocabulary but not its meaning: a release is announced and
    /// retracted where a version is cut and pulled. Nothing cuts a release, but reverting one returns
    /// it to <c>Ready</c>, so <c>Ready</c> is required for the same reason it is on a version.
    /// <para>
    /// A release reaches <c>Ready</c> only by being reverted, and the moves out of it are required
    /// whatever the workflow: a reverted release must still be able to be announced again or
    /// withdrawn. Reverting and withdrawing a released release are optional.
    /// </para>
    /// </remarks>
    public static readonly WorkflowOwnerDescriptor Release = new(
        "delivery.release",
        "Release",
        DeliveryNames,
        [(int)ProductStatusAlias.Ready, (int)ProductStatusAlias.Released, (int)ProductStatusAlias.Withdrawn],
        DeliveryCategories,
        supportsRestrictedTransitions: true,
        moves:
        [
            WorkflowMove.Required("Mark Released", Initial, (int)ProductStatusAlias.Released),
            WorkflowMove.Required("Mark Released", (int)ProductStatusAlias.Ready, (int)ProductStatusAlias.Released),
            WorkflowMove.Optional("Revert Release", (int)ProductStatusAlias.Released, (int)ProductStatusAlias.Ready),
            WorkflowMove.Required("Withdraw", Initial, (int)ProductStatusAlias.Withdrawn),
            WorkflowMove.Required("Withdraw", (int)ProductStatusAlias.Ready, (int)ProductStatusAlias.Withdrawn),
            WorkflowMove.Optional("Withdraw", (int)ProductStatusAlias.Released, (int)ProductStatusAlias.Withdrawn),
        ]);

    /// <summary>A coordinated shipment of several component releases.</summary>
    /// <remarks>
    /// No action moves a package into <c>Ready</c>, but a reassignment can map one there, and releasing
    /// or withdrawing it from there is what the domain allows. Those moves are optional because
    /// <c>Ready</c> is: a workflow that keeps the status must allow at least one of them, or publish
    /// refuses it as a dead end. Withdrawing a released package is optional.
    /// <para>
    /// For the same reason Active is not a required category: Ready is the only Active status a package
    /// workflow can usefully hold, so requiring the category would require Ready.
    /// </para>
    /// </remarks>
    public static readonly WorkflowOwnerDescriptor ReleasePackage = new(
        "delivery.release-package",
        "Release Package",
        DeliveryNames,
        [(int)ProductStatusAlias.Released, (int)ProductStatusAlias.Withdrawn],
        [StatusCategory.Proposed, StatusCategory.Done, StatusCategory.Removed],
        supportsRestrictedTransitions: true,
        moves:
        [
            WorkflowMove.Required("Mark Released", Initial, (int)ProductStatusAlias.Released),
            WorkflowMove.Required("Withdraw", Initial, (int)ProductStatusAlias.Withdrawn),
            WorkflowMove.Optional("Mark Released", (int)ProductStatusAlias.Ready, (int)ProductStatusAlias.Released),
            WorkflowMove.Optional("Withdraw", (int)ProductStatusAlias.Ready, (int)ProductStatusAlias.Withdrawn),
            WorkflowMove.Optional("Withdraw", (int)ProductStatusAlias.Released, (int)ProductStatusAlias.Withdrawn),
        ]);

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
    /// <para>
    /// Every move is required for the same reason as the aliases, so a restricted deployment workflow
    /// always records each outcome. A deployment starts in <c>InProgress</c> rather than the initial
    /// status, so its moves start there.
    /// </para>
    /// </remarks>
    public static readonly WorkflowOwnerDescriptor Deployment = new(
        "delivery.deployment",
        "Deployment",
        Names(
            (ProductStatusAlias.InProgress, StatusCategory.Active),
            (ProductStatusAlias.Succeeded, StatusCategory.Done),
            (ProductStatusAlias.Failed, StatusCategory.Removed),
            (ProductStatusAlias.RolledBack, StatusCategory.Removed)),
        [
            (int)ProductStatusAlias.InProgress,
            (int)ProductStatusAlias.Succeeded,
            (int)ProductStatusAlias.Failed,
            (int)ProductStatusAlias.RolledBack,
        ],
        [StatusCategory.Active, StatusCategory.Done, StatusCategory.Removed],
        supportsRestrictedTransitions: true,
        moves:
        [
            WorkflowMove.Required("Record Success", (int)ProductStatusAlias.InProgress, (int)ProductStatusAlias.Succeeded),
            WorkflowMove.Required("Record Failure", (int)ProductStatusAlias.InProgress, (int)ProductStatusAlias.Failed),
            WorkflowMove.Required("Roll Back", (int)ProductStatusAlias.Succeeded, (int)ProductStatusAlias.RolledBack),
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

    private static Dictionary<int, WorkflowAlias> Names(params (ProductStatusAlias Alias, StatusCategory Category)[] aliases) =>
        aliases.ToDictionary(a => (int)a.Alias, a => new WorkflowAlias(a.Alias.ToString(), a.Category));
}
