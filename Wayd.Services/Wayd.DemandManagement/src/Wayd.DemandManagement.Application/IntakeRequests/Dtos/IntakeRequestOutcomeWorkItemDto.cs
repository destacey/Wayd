using Wayd.Common.Domain.Enums.Work;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// A work item an intake request's outcome points at, as Work reports it now.
/// </summary>
/// <remarks>
/// Demand Management keeps no copy of work items: the API layer asks Work for these fields when it adds an
/// outcome or shows a request, and hands them in.
/// </remarks>
public sealed record IntakeRequestOutcomeWorkItemDto
{
    /// <summary>The work item's id, the same as in Work.</summary>
    public Guid Id { get; init; }

    /// <summary>The work item's key, which its page is addressed by.</summary>
    public required string Key { get; init; }

    /// <summary>The work item's title.</summary>
    public required string Title { get; init; }

    /// <summary>The key of the workspace the work item is in, which its page is addressed by.</summary>
    public required string WorkspaceKey { get; init; }

    /// <summary>The work item's status, by name.</summary>
    public required string Status { get; init; }

    /// <summary>The category of the work item's status.</summary>
    public WorkStatusCategory StatusCategory { get; init; }
}
