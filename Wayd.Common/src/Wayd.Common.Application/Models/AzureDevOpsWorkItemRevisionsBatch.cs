using Wayd.Common.Application.Interfaces.ExternalWork;

namespace Wayd.Common.Application.Models;

/// <summary>
/// One page of a project's work item revisions from the Azure DevOps reporting revisions API.
/// </summary>
/// <param name="Revisions">The page's revisions, in the order Azure DevOps returned them.</param>
/// <param name="ContinuationToken">
/// Where the next page starts. Azure DevOps returns one on the last page too, so a later read
/// resumes with only the revisions made since; null when it returned none.
/// </param>
/// <param name="IsLastBatch">Whether Azure DevOps has no more revisions to return for now.</param>
public sealed record AzureDevOpsWorkItemRevisionsBatch(
    IReadOnlyList<IExternalWorkItemRevision> Revisions,
    string? ContinuationToken,
    bool IsLastBatch);
