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
    /// <summary>The story points estimate; null when the item has none.</summary>
    double? StoryPoints { get; }

    /// <summary>The effort estimate; null when the item has none.</summary>
    double? Effort { get; }

    /// <summary>The size estimate; null when the item has none.</summary>
    double? Size { get; }

    IReadOnlyCollection<string> Tags { get; }
}
