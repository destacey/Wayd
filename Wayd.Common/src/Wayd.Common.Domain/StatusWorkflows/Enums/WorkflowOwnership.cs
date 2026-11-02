namespace Wayd.Common.Domain.StatusWorkflows.Enums;

/// <summary>
/// Who keeps a piece of workflow configuration: Wayd's administrators, or a source system's sync.
/// </summary>
/// <remarks>
/// Stored by name, so the values may be reordered but never renamed.
/// </remarks>
public enum WorkflowOwnership
{
    /// <summary>Defined and edited in Wayd.</summary>
    Owned = 1,

    /// <summary>
    /// Kept by a sync from the source system it mirrors, which it identifies by an external key. Its
    /// structure follows the source; administrators edit only what Wayd adds.
    /// </summary>
    Managed = 2,
}
