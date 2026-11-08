using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// A project deliverable an existing project scope outcome is mapped to, as Demand Management's copy holds it.
/// Summary tier, like the outcome.
/// </summary>
public sealed record IntakeRequestOutcomeDeliverableDto
{
    /// <summary>The deliverable, by its id in PPM.</summary>
    public Guid Id { get; init; }

    /// <summary>What the deliverable is called; null once it has been deleted from the project.</summary>
    public string? Name { get; init; }

    /// <summary>The category of the deliverable's status; null once it has been deleted from the project.</summary>
    public StatusCategory? StatusCategory { get; init; }

    /// <summary>
    /// The well-known meaning of the deliverable's status — <c>Accepted</c> is what the request waits for, and
    /// <c>Descoped</c> means it was dropped; null once it has been deleted from the project.
    /// </summary>
    public ProjectDeliverableStatusAlias? StatusAlias { get; init; }

    /// <summary>
    /// Whether the deliverable is still on the project. One deleted before work on the project began stays on the
    /// outcome as the record of what was mapped, and does not count toward whether the outcome is met.
    /// </summary>
    public bool IsOnProject { get; init; }

    /// <summary>The deliverable as its copy holds it, or as deleted when the copy has gone.</summary>
    public static IntakeRequestOutcomeDeliverableDto From(Guid id, DemandProjectDeliverable? copy) =>
        copy is null
            ? new() { Id = id, IsOnProject = false }
            : new()
            {
                Id = id,
                Name = copy.Name,
                StatusCategory = copy.StatusCategory,
                StatusAlias = copy.StatusAlias,
                IsOnProject = true,
            };
}
