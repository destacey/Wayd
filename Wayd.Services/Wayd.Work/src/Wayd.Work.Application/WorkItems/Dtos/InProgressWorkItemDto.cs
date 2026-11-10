namespace Wayd.Work.Application.WorkItems.Dtos;

/// <summary>A work item in progress, with when it started.</summary>
public sealed record InProgressWorkItemDto
{
    /// <summary>The work item's id.</summary>
    public required Guid Id { get; init; }

    /// <summary>The work item's key, such as <c>WAYD-123</c>, which its page is addressed by.</summary>
    public required string Key { get; init; }

    /// <summary>The key of the workspace the work item is in, which its page is addressed under.</summary>
    public required string WorkspaceKey { get; init; }

    /// <summary>The work item's title.</summary>
    public required string Title { get; init; }

    /// <summary>When the work item first entered an In Progress-category status.</summary>
    public required Instant ActivatedOn { get; init; }
}
