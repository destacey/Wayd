using System.Linq.Expressions;
using Wayd.Common.Application.Models.Organizations;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkTeams.Allocation;
using Wayd.Work.Application.WorkTeams.Dtos;

namespace Wayd.Work.Application.WorkTeams.Queries;

/// <summary>
/// Where the Requirement-tier work a team, or a team of teams and everything beneath it, completed from
/// <paramref name="From"/> to <paramref name="To"/> (inclusive, UTC dates) went.
/// </summary>
public sealed record GetTeamAllocationQuery(
    TeamIdOrCode TeamIdOrCode,
    LocalDate From,
    LocalDate To,
    AllocationOptions Options) : IQuery<Result<TeamAllocationDto?>>, ILongRunningRequest;

public sealed class GetTeamAllocationQueryValidator : AbstractValidator<GetTeamAllocationQuery>
{
    /// <summary>A year, both ends inclusive, with room for a leap day.</summary>
    public const int MaxDays = 366;

    public GetTeamAllocationQueryValidator()
    {
        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage("The end date must not be before the start date.");

        RuleFor(q => q)
            .Must(q => Period.Between(q.From, q.To, PeriodUnits.Days).Days + 1 <= MaxDays)
            .WithMessage($"The date range must be at most {MaxDays} days.");

        RuleFor(q => q.Options).NotNull();
        RuleFor(q => q.Options.Dimension).IsInEnum();
        RuleFor(q => q.Options.Measure).IsInEnum();
        RuleFor(q => q.Options.Unestimated).IsInEnum();
        RuleFor(q => q.Options.ThemeCounting).IsInEnum();
    }
}

public sealed class GetTeamAllocationQueryHandler(IWorkDbContext workDbContext, IDispatcher dispatcher)
    : IQueryHandler<GetTeamAllocationQuery, Result<TeamAllocationDto?>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;

    public async Task<Result<TeamAllocationDto?>> Handle(GetTeamAllocationQuery request, CancellationToken cancellationToken)
    {
        var team = await _workDbContext.WorkTeams
            .Where(request.TeamIdOrCode.Match<Expression<Func<WorkTeam, bool>>>(
                id => t => t.Id == id,
                code => t => t.Code == code))
            .ProjectToType<WorkTeamNavigationDto>()
            .FirstOrDefaultAsync(cancellationToken);

        if (team is null)
            return Result.Success<TeamAllocationDto?>(null);

        // Work teams share their Organization team's id.
        var structure = await _dispatcher.Send(new GetTeamStructureQuery(team.Id, request.From, request.To), cancellationToken);
        if (structure is null)
            return Result.Success<TeamAllocationDto?>(null);

        var teamIds = structure.Teams.Select(t => t.Id).ToList();
        var start = request.From.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();
        var end = request.To.PlusDays(1).AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();

        // Removed items also carry a DoneTimestamp, so the status category is what excludes them. Only the
        // Requirement tier counts, so a feature is never counted on top of the stories beneath it.
        var completions = await _workDbContext.WorkItems
            .Where(w => w.TeamId.HasValue && teamIds.Contains(w.TeamId.Value))
            .Where(w => w.Type.Level!.Tier == WorkTypeTier.Requirement)
            .Where(w => w.StatusCategory == WorkStatusCategory.Done)
            .Where(w => w.DoneTimestamp >= start && w.DoneTimestamp < end)
            .Select(w => new
            {
                w.Id,
                TeamId = w.TeamId!.Value,
                TypeName = w.Type.Name,
                DoneTimestamp = w.DoneTimestamp!.Value,
                ProjectId = w.ProjectId ?? w.ParentProjectId,
                w.StoryPoints,
            })
            .ToListAsync(cancellationToken);

        var workItems = completions
            .Select(c => new AllocationWorkItem(c.Id, c.TeamId, c.TypeName, c.DoneTimestamp.InUtc().Date, c.ProjectId, c.StoryPoints))
            .ToList();

        var projectIds = workItems.Where(w => w.ProjectId.HasValue).Select(w => w.ProjectId!.Value).Distinct().ToList();
        var projects = projectIds.Count == 0
            ? []
            : (await _dispatcher.Send(new GetProjectClassificationsQuery(projectIds), cancellationToken))
                .ToDictionary(p => p.ProjectId);

        return AllocationCalculator.Calculate(team, request.From, request.To, structure, workItems, projects, request.Options);
    }
}
