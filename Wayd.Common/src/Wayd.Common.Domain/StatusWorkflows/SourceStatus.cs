using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// One status as a source system reports it, for <see cref="StatusWorkflow.SyncFromSource"/> to mirror in a
/// managed workflow.
/// </summary>
/// <param name="ExternalKey">
/// The source's stable identifier for the status: an Azure DevOps state name within its work item type, a
/// Jira status id, a Monday label id. Matched without regard to case.
/// </param>
/// <param name="Name">What the source calls the status now.</param>
/// <param name="Category">
/// The category the source suggests. Used only when the status first appears: from then on the category is
/// the administrators' mapping, and the sync never overwrites it.
/// </param>
/// <param name="IsHidden">
/// Whether the source keeps the status but no longer offers it, as Azure DevOps does with a hidden state. It
/// is kept as a retired status, so records and history that hold it still resolve to it and its category.
/// </param>
public sealed record SourceStatus(string ExternalKey, string Name, StatusCategory Category, bool IsHidden = false);
