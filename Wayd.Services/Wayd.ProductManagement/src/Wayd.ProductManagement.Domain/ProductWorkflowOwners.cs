using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.ProductManagement.Domain;

/// <summary>
/// The kinds of Product Management record the status workflow engine governs, and what each one needs
/// from a workflow before it can be activated.
/// </summary>
/// <remarks>
/// <para>
/// Declared here rather than in the engine so Common carries no Product Management vocabulary. A module
/// adopting the engine later writes its own equivalent of this file and changes nothing in
/// <c>Wayd.Common.Domain</c>.
/// </para>
/// <para>
/// The keys are persisted on every workflow row. <strong>They must never change</strong> — renaming one
/// orphans every workflow that carries it — which is why they are namespaced and declared once here
/// rather than written as literals at call sites.
/// </para>
/// </remarks>
public static class ProductWorkflowOwners
{
    /// <summary>A node in the product taxonomy. Governs the product lifecycle.</summary>
    public static readonly WorkflowOwnerDescriptor Product = new(
        "product.product",
        "Product",
        [(int)ProductStatusAlias.Active, (int)ProductStatusAlias.Retired],
        Describe);

    /// <summary>A versioned cut of a releasable product node.</summary>
    public static readonly WorkflowOwnerDescriptor Release = new(
        "product.release",
        "Release",
        [(int)ProductStatusAlias.Released, (int)ProductStatusAlias.Withdrawn],
        Describe);

    /// <summary>A coordinated shipment of several component releases.</summary>
    public static readonly WorkflowOwnerDescriptor ReleasePackage = new(
        "product.release-package",
        "Release Package",
        [(int)ProductStatusAlias.Released, (int)ProductStatusAlias.Withdrawn],
        Describe);

    /// <summary>
    /// One release or package reaching one environment. Governs the deployment outcome.
    /// </summary>
    /// <remarks>
    /// The strictest of the four, and deliberately so. Change failure rate is
    /// <c>(Failed + RolledBack) / total</c> and time-to-restore measures from a failure to the next
    /// success, so an organization that could omit or rename those outcomes without an alias would make
    /// both uncomputable and non-comparable between organizations. Configurable outcomes are fine;
    /// optional <em>meanings</em> are not.
    /// </remarks>
    public static readonly WorkflowOwnerDescriptor Deployment = new(
        "product.deployment",
        "Deployment",
        [(int)ProductStatusAlias.Succeeded, (int)ProductStatusAlias.Failed, (int)ProductStatusAlias.RolledBack],
        Describe);

    /// <summary>
    /// Every owner type this module contributes, for registration at startup.
    /// </summary>
    public static WorkflowOwnerDescriptor[] All => [Product, Release, ReleasePackage, Deployment];

    /// <summary>
    /// Registers this module's owner types with the engine. Call once during startup, before anything
    /// resolves a workflow.
    /// </summary>
    public static void Register() => WorkflowOwners.Register(All);

    private static string Describe(int alias) =>
        Enum.IsDefined((ProductStatusAlias)alias) ? ((ProductStatusAlias)alias).ToString() : alias.ToString();
}
