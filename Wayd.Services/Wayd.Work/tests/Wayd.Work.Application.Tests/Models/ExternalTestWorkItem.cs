using Wayd.Common.Application.Interfaces.ExternalWork;
using NodaTime;

namespace Wayd.Work.Application.Tests.Models;

public class ExternalTestWorkItem : IExternalWorkItem
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string WorkType { get; set; } = default!;
    public string WorkStatus { get; set; } = default!;
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
    public string? ExternalTeamIdentifier { get; set; }
    public Guid? TeamId { get; set; }
    public int? IterationId { get; set; }
    public double? StoryPoints { get; set; }
    public IReadOnlyCollection<string> Tags { get; set; } = [];
}
