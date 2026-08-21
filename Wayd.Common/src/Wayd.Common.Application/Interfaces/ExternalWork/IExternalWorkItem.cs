namespace Wayd.Common.Application.Interfaces.ExternalWork;

public interface IExternalWorkItem
{
    int Id { get; }
    string Title { get; }
    string WorkType { get; }
    string WorkStatus { get; }
    int? ParentId { get; }
    IExternalUserRef? AssignedTo { get; }
    Instant Created { get; }
    IExternalUserRef? CreatedBy { get; }
    Instant LastModified { get; }
    IExternalUserRef? LastModifiedBy { get; }
    int? Priority { get; }
    double StackRank { get; }
    Instant? ActivatedTimestamp { get; }
    Instant? DoneTimestamp { get; }
    public Guid? TeamId { get; set; }
    string? ExternalTeamIdentifier { get; }
    int? IterationId { get; }
    double? StoryPoints { get; }
    IReadOnlyCollection<string> Tags { get; }
}
