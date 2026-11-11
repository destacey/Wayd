using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Dtos;

/// <summary>An open deliverable the caller is the owner of, with the project it belongs to.</summary>
public sealed record MyProjectDeliverableDto
{
    /// <summary>The deliverable's id.</summary>
    public Guid Id { get; init; }

    /// <summary>What the deliverable is called.</summary>
    public required string Name { get; init; }

    /// <summary>When the deliverable is due, if a date has been committed to.</summary>
    public LocalDate? TargetDate { get; init; }

    /// <summary>A status in the Proposed or Active category: the list holds open deliverables only.</summary>
    public required StatusNavigationDto Status { get; init; }

    /// <summary>The project the deliverable belongs to.</summary>
    public required ProjectNavigationDto Project { get; init; }
}
