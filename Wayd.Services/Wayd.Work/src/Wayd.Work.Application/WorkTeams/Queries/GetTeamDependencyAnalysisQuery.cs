using System.Linq.Expressions;
using Wayd.Common.Application.Models.Organizations;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.WorkTeams.DependencyAnalysis;
using Wayd.Work.Application.WorkTeams.Dtos;

namespace Wayd.Work.Application.WorkTeams.Queries;

/// <summary>
/// Where the dependencies of a team, or of every team beneath a team of teams, ran from <paramref name="From"/>
/// to <paramref name="To"/> (inclusive, UTC dates). Null when the team does not exist.
/// </summary>
public sealed record GetTeamDependencyAnalysisQuery(TeamIdOrCode TeamIdOrCode, LocalDate From, LocalDate To)
    : IQuery<Result<TeamDependencyAnalysisDto?>>, ILongRunningRequest;

/// <summary>Validates <see cref="GetTeamDependencyAnalysisQuery"/>.</summary>
public sealed class GetTeamDependencyAnalysisQueryValidator : AbstractValidator<GetTeamDependencyAnalysisQuery>
{
    /// <summary>Two years, both ends inclusive, with room for a leap day.</summary>
    public const int MaxDays = 731;

    /// <summary>Requires the end not before the start, and the range at most <see cref="MaxDays"/> days.</summary>
    public GetTeamDependencyAnalysisQueryValidator()
    {
        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage("The end date must not be before the start date.");

        RuleFor(q => q)
            .Must(q => Period.Between(q.From, q.To, PeriodUnits.Days).Days + 1 <= MaxDays)
            .WithMessage($"The date range must be at most {MaxDays} days.");
    }
}

/// <summary>Handles <see cref="GetTeamDependencyAnalysisQuery"/>.</summary>
public sealed class GetTeamDependencyAnalysisQueryHandler(IWorkDbContext workDbContext, IDispatcher dispatcher)
    : IQueryHandler<GetTeamDependencyAnalysisQuery, Result<TeamDependencyAnalysisDto?>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;

    /// <summary>
    /// Covers every team beneath a team of teams at any point in the range. Removed links and Intra-Team
    /// dependencies are left out; a dependency with an end that has no team is kept, because its scope is
    /// unknown rather than known to be within one team.
    /// </summary>
    public async Task<Result<TeamDependencyAnalysisDto?>> Handle(GetTeamDependencyAnalysisQuery request, CancellationToken cancellationToken)
    {
        var team = await _workDbContext.WorkTeams
            .Where(request.TeamIdOrCode.Match<Expression<Func<WorkTeam, bool>>>(
                id => t => t.Id == id,
                code => t => t.Code == code))
            .ProjectToType<WorkTeamNavigationDto>()
            .FirstOrDefaultAsync(cancellationToken);

        if (team is null)
            return Result.Success<TeamDependencyAnalysisDto?>(null);

        // Work teams share their Organization team's id.
        var structure = await _dispatcher.Send(new GetTeamStructureQuery(team.Id, request.From, request.To), cancellationToken);
        if (structure is null)
            return Result.Success<TeamDependencyAnalysisDto?>(null);

        // Teams of teams own no work items.
        var teamIds = structure.Teams
            .Where(t => t.Type == TeamType.Team)
            .Select(t => t.Id)
            .ToHashSet();

        var start = request.From.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();
        var end = request.To.PlusDays(1).AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();
        var scope = teamIds.ToArray();

        // Leaves out what closed before the range began (see ClosedOn): a link made before it, closed by ends whose
        // done timestamps are before it or missing. A link made inside the range always counts.
        var dtos = await _workDbContext.WorkItemDependencies
            .Where(d => d.RemovedOn == null)
            .Where(d => d.CreatedOn < end)
            .Where(d => scope.Contains(d.Source!.TeamId!.Value) || scope.Contains(d.Target!.TeamId!.Value))
            .Where(d => d.Source!.TeamId == null || d.Target!.TeamId == null || d.Source.TeamId != d.Target.TeamId)
            .Where(d => d.CreatedOn >= start
                || !((d.Source!.StatusCategory == StatusCategory.Done
                        && (d.Source.DoneTimestamp == null || d.Source.DoneTimestamp < start))
                    || (d.Source.StatusCategory == StatusCategory.Removed
                        && (d.Target!.StatusCategory == StatusCategory.Done || d.Target.StatusCategory == StatusCategory.Removed)
                        && (d.Source.DoneTimestamp == null || d.Source.DoneTimestamp < start)
                        && (d.Target.DoneTimestamp == null || d.Target.DoneTimestamp < start))))
            .ProjectToType<DependencyDto>()
            .ToListAsync(cancellationToken);

        var items = dtos.Select(d => new DependencyAnalysisItem(d, ClosedOn(d)));

        return DependencyAnalysisCalculator.Calculate(team, request.From, request.To, teamIds, items);
    }

    /// <summary>
    /// When the dependency closed, read from the same row as the rest of it: when its predecessor was done, or,
    /// for a removed predecessor, when the later of the two ends finished once the successor is done or removed
    /// too. A removed predecessor with an open successor stays open: the successor still needs work nobody will do.
    /// A removed item carries a done timestamp too. Ends with none are taken to have closed when the link was made,
    /// a fixed point that a later edit to an item cannot move.
    /// </summary>
    private static Instant? ClosedOn(DependencyDto dependency)
    {
        var source = dependency.Source;
        var target = dependency.Target;

        if (source.StatusCategory.Id == (int)StatusCategory.Done)
            return source.DoneTimestamp ?? dependency.CreatedOn;

        if (source.StatusCategory.Id == (int)StatusCategory.Removed
            && target.StatusCategory.Id is (int)StatusCategory.Done or (int)StatusCategory.Removed)
            return Later(source.DoneTimestamp, target.DoneTimestamp) ?? dependency.CreatedOn;

        return null;
    }

    private static Instant? Later(Instant? a, Instant? b) =>
        a is null ? b : b is null ? a : Instant.Max(a.Value, b.Value);
}
