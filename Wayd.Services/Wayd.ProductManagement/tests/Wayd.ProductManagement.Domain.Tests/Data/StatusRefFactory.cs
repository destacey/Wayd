using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.ProductManagement.Domain.Tests.Data;

/// <summary>
/// Builds the resolved statuses aggregates take, without standing up a whole workflow.
/// </summary>
/// <remarks>
/// A plain factory rather than a faker: a <see cref="StatusRef"/> is a value the application layer
/// resolves and hands in, not an entity with persisted state, so there is nothing for Bogus to bind.
/// </remarks>
internal static class StatusRefFactory
{
    /// <summary>
    /// A resolved status. Every call invents its own workflow id unless one is supplied, so tests that
    /// do not care about the workflow are unaffected by it.
    /// </summary>
    public static StatusRef For(
        StatusCategory category,
        ProductStatusAlias alias = ProductStatusAlias.None,
        Guid? workflowId = null,
        string? name = null) =>
        new(workflowId ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            name ?? (alias == ProductStatusAlias.None ? category.ToString() : alias.ToString()),
            category,
            (int)alias);

    public static StatusRef Ready() => For(StatusCategory.Active, ProductStatusAlias.Ready);

    public static StatusRef Released() => For(StatusCategory.Done, ProductStatusAlias.Released);

    public static StatusRef Withdrawn() => For(StatusCategory.Removed, ProductStatusAlias.Withdrawn);

    public static StatusRef InProgress() => For(StatusCategory.Active, ProductStatusAlias.InProgress);

    public static StatusRef Succeeded() => For(StatusCategory.Done, ProductStatusAlias.Succeeded);

    public static StatusRef Failed() => For(StatusCategory.Removed, ProductStatusAlias.Failed);

    public static StatusRef RolledBack() => For(StatusCategory.Removed, ProductStatusAlias.RolledBack);

    public static StatusRef Retired() => For(StatusCategory.Done, ProductStatusAlias.Retired);
}
