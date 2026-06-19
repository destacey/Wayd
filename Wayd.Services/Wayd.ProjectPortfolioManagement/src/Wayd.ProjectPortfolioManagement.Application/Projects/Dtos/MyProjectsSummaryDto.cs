namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

/// <summary>
/// Summary of the current user's project involvement, with counts per role.
/// </summary>
public sealed record MyProjectsSummaryDto
{
    public int TotalCount { get; init; }
    public int SponsorCount { get; init; }
    public int OwnerCount { get; init; }
    public int ManagerCount { get; init; }
    public int MemberCount { get; init; }
    public int AssigneeCount { get; init; }
}
