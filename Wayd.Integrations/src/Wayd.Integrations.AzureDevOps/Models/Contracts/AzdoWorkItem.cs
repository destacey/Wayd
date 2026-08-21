using Wayd.Common.Application.Interfaces.ExternalWork;
using NodaTime;

namespace Wayd.Integrations.AzureDevOps.Models.Contracts;

public sealed record AzdoWorkItem : IExternalWorkItem
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string WorkType { get; set; }
    public required string WorkStatus { get; set; }
    public int? ParentId { get; set; }
    public IExternalUserRef? AssignedTo { get; set; }
    public Instant Created { get; set; }
    public IExternalUserRef? CreatedBy { get; set; }
    public Instant LastModified { get; set; }
    public IExternalUserRef? LastModifiedBy { get; set; }
    public int? Priority { get; set; }
    public double StackRank { get; set; }
    public Instant? ActivatedTimestamp { get; set; }
    public Instant? DoneTimestamp { get; set; }
    public Guid? TeamId { get; set; }
    public string? ExternalTeamIdentifier { get; set; }
    public int? IterationId { get; set; }
    public double? StoryPoints { get; set; }
    public IReadOnlyCollection<string> Tags { get; set; } = [];
}
