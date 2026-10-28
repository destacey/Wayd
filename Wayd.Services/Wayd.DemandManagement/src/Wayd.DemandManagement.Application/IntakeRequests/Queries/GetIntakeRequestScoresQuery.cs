using Wayd.Common.Application.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// The current scores of the requests the requests list would return for the same filters, for the requests
/// whose score the current user may read. A request never scored, or whose substance they may not see, is
/// absent.
/// </summary>
/// <remarks>
/// Answered beside <see cref="GetIntakeRequestsQuery"/> rather than on its summaries, because a score is
/// substance and a summary carries nothing from that tier.
/// </remarks>
/// <param name="DeskId">Only requests on this desk, when given.</param>
/// <param name="Mine">Only requests the current user is the requester of or submitted.</param>
/// <param name="OpenOnly">Only requests in the Proposed or Active status category.</param>
public sealed record GetIntakeRequestScoresQuery(Guid? DeskId = null, bool Mine = false, bool OpenOnly = false)
    : IQuery<List<IntakeRequestCurrentScoreDto>>;

public sealed class GetIntakeRequestScoresQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetIntakeRequestScoresQuery, List<IntakeRequestCurrentScoreDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<List<IntakeRequestCurrentScoreDto>> Handle(GetIntakeRequestScoresQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        var desks = _dbContext.IntakeDesks;

        var query = IntakeRequestListFilter.Apply(
            _dbContext.IntakeRequests.AsNoTracking()
                .Where(reader.CanSeeSummary(desks))
                .Where(reader.CanSeeSubstance(desks))
                .Where(r => r.CurrentScore != null),
            reader, request.DeskId, request.Mine, request.OpenOnly);
        if (query is null)
            return [];

        var scoringModels = _dbContext.ScoringModels;

        return await query
            .OrderBy(r => r.Key)
            .Select(r => new IntakeRequestCurrentScoreDto
            {
                RequestId = r.Id,
                Value = r.CurrentScore!.Value,
                ScoredOn = r.CurrentScore.ScoredOn,
                ScoringModel = new NavigationDto
                {
                    Id = r.CurrentScore.ScoringModelId,
                    Key = scoringModels.Where(m => m.Id == r.CurrentScore.ScoringModelId).Select(m => m.Key).FirstOrDefault(),
                    Name = r.CurrentScore.ScoringModelName,
                },
                IsDeskModel = desks.Any(d => d.Id == r.DeskId && d.ScoringModelId == r.CurrentScore.ScoringModelId),
            })
            .ToListAsync(cancellationToken);
    }
}
