using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.DemandManagement.Domain.Tests.Data;

/// <summary>
/// Builds the resolved statuses an intake request takes, without standing up a whole workflow.
/// </summary>
/// <remarks>
/// A plain factory rather than a faker: a <see cref="StatusRef"/> is a value the application layer resolves
/// and hands in, not an entity with persisted state.
/// </remarks>
public static class IntakeStatusRefFactory
{
    /// <summary>
    /// A resolved status. Every call invents its own workflow id unless one is supplied, so tests that do not
    /// care about the workflow are unaffected by it.
    /// </summary>
    public static StatusRef For(
        StatusCategory category,
        IntakeRequestStatusAlias alias = IntakeRequestStatusAlias.None,
        Guid? workflowId = null,
        string? name = null) =>
        new(workflowId ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            name ?? (alias == IntakeRequestStatusAlias.None ? category.ToString() : alias.ToString()),
            category,
            (int)alias);

    public static StatusRef Submitted(Guid? workflowId = null) =>
        For(StatusCategory.Proposed, IntakeRequestStatusAlias.Submitted, workflowId);
}
