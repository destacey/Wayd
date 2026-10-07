using Wayd.Common.Application.Interfaces.ExternalWork;
using NodaTime;

namespace Wayd.Integrations.AzureDevOps.Models.Contracts;

/// <summary>
/// A work item revision as the Azure DevOps reporting revisions API recorded it. The team is not
/// on the item in Azure DevOps but follows from the iteration, so <see cref="TeamKey"/> is always null.
/// </summary>
public sealed record AzdoWorkItemRevision : IExternalWorkItemRevision
{
    public int WorkItemId { get; init; }
    public int Revision { get; init; }
    public Instant Changed { get; init; }
    public required string WorkType { get; init; }
    public required string WorkStatus { get; init; }
    public int? IterationId { get; init; }
    public string? TeamKey => null;
    public IExternalUserRef? AssignedTo { get; init; }
    public double? StoryPoints { get; init; }
    public double? Effort { get; init; }
    public double? Size { get; init; }
}
