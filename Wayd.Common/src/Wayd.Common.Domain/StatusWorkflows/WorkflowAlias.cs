using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// One well-known meaning an owner type's statuses can carry: what it is called, and the category a status
/// carrying it must belong to.
/// </summary>
/// <remarks>
/// The category is part of the meaning. A module's rules read both — a withdrawn request is closed because
/// Withdrawn is a Removed status — so a workflow that filed Withdrawn under Proposed would leave a withdrawn
/// request open. The editor refuses that pairing rather than each module guarding against it at runtime.
/// </remarks>
/// <param name="Name">What to call the meaning in a message or an admin screen.</param>
/// <param name="Category">The only category a status carrying this meaning may belong to.</param>
public sealed record WorkflowAlias(string Name, StatusCategory Category);
