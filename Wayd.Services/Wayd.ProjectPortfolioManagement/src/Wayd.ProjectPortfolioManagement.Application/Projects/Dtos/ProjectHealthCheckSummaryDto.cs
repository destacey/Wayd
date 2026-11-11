using Wayd.Common.Application.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

/// <summary>A project's current health check, as a list shows it.</summary>
public sealed record ProjectHealthCheckSummaryDto
{
    public Guid Id { get; set; }
    public required SimpleNavigationDto Status { get; set; }

    /// <summary>When the health check stops being current, after which the project has none until another is reported.</summary>
    public Instant Expiration { get; set; }
}
