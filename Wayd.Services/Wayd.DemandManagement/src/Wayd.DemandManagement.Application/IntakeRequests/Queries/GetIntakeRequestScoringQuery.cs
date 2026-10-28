using Mapster;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Scoring.ScoringModels.Dtos;
using Wayd.Common.Domain.Scoring.Enums;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// What an intake request's Score tab shows, or null when the current user may not read the request's
/// substance: a score they may not see is reported exactly like one that is not there.
/// </summary>
/// <param name="Id">The request.</param>
public sealed record GetIntakeRequestScoringQuery(Guid Id) : IQuery<IntakeRequestScoringDto?>;

public sealed class GetIntakeRequestScoringQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetIntakeRequestScoringQuery, IntakeRequestScoringDto?>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<IntakeRequestScoringDto?> Handle(GetIntakeRequestScoringQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        var desks = _dbContext.IntakeDesks;
        var readable = _dbContext.IntakeRequests.AsNoTracking()
            .Where(reader.CanSeeSummary(desks))
            .Where(reader.CanSeeSubstance(desks));

        var facts = await readable
            .Where(r => r.Id == request.Id)
            .Select(r => new
            {
                r.DeskId,
                r.StatusCategory,
                DeskModelId = desks.Where(d => d.Id == r.DeskId).Select(d => d.ScoringModelId).First(),
                CurrentValue = r.CurrentScore == null ? (decimal?)null : r.CurrentScore.Value,
                CurrentModelId = r.CurrentScore == null ? (Guid?)null : r.CurrentScore.ScoringModelId,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (facts is null)
            return null;

        var model = facts.DeskModelId is { } modelId
            ? await _dbContext.FindForScoring(modelId, cancellationToken)
            : null;
        var isOpen = facts.StatusCategory is StatusCategory.Proposed or StatusCategory.Active;

        return new IntakeRequestScoringDto
        {
            ScoringModel = model?.Adapt<ScoringModelDetailsDto>(),
            ScoringModelArchived = model?.State == ScoringModelState.Archived,
            CanScore = !_currentPrincipal.IsReadOnly && reader.EmployeeId is not null && reader.IsAgentOn(facts.DeskId)
                && isOpen && model is not null,
            Scores = await Scores(request.Id, cancellationToken),
            DeskRank = isOpen && facts.CurrentValue is { } value && facts.CurrentModelId == facts.DeskModelId
                ? await DeskRank(readable, facts.DeskId, facts.DeskModelId!.Value, value, cancellationToken)
                : null,
        };
    }

    private Task<List<IntakeRequestScoreDto>> Scores(Guid requestId, CancellationToken cancellationToken)
    {
        var employees = _dbContext.Employees;

        return _dbContext.IntakeRequests.AsNoTracking()
            .Where(r => r.Id == requestId)
            .SelectMany(r => r.Scores)
            .OrderByDescending(s => s.Sequence)
            .Select(s => new IntakeRequestScoreDto
            {
                Id = s.Id,
                Sequence = s.Sequence,
                PrimaryValue = s.PrimaryValue,
                ScoredOn = s.ScoredOn,
                ScoredBy = employees.Where(e => e.Id == s.ScoredById)
                    .Select(e => new EmployeeNavigationDto { Id = e.Id, Key = e.Key, Name = e.Name.FirstName + " " + e.Name.LastName })
                    .First(),
                ScoringModel = new NavigationDto { Id = s.ScoringModelId, Key = s.ScoringModelKey, Name = s.ScoringModelName },
                Ratings = s.Ratings.OrderBy(x => x.Order)
                    .Select(x => new IntakeRequestScoreRatingDto
                    {
                        CriterionId = x.CriterionId,
                        CriterionName = x.CriterionName,
                        CriterionToken = x.CriterionToken,
                        RatingValue = x.RatingValue,
                        RatingLevelId = x.RatingLevelId,
                        RatingLevelLabel = x.RatingLevelLabel,
                        Order = x.Order,
                    })
                    .ToList(),
                Outputs = s.Outputs.OrderBy(x => x.Order)
                    .Select(x => new IntakeRequestScoreOutputDto
                    {
                        Token = x.Token,
                        Name = x.Name,
                        Value = x.Value,
                        IsPrimary = x.IsPrimary,
                        Order = x.Order,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The request's place among the open requests on its desk scored with the desk's model, over the requests
    /// whose scores the reader may read.
    /// </summary>
    private static async Task<IntakeRequestDeskRankDto> DeskRank(IQueryable<Domain.Models.IntakeRequest> readable, Guid deskId,
        Guid deskModelId, decimal value, CancellationToken cancellationToken)
    {
        var ranked = readable.Where(r => r.DeskId == deskId
            && (r.StatusCategory == StatusCategory.Proposed || r.StatusCategory == StatusCategory.Active)
            && r.CurrentScore != null
            && r.CurrentScore.ScoringModelId == deskModelId);

        return new IntakeRequestDeskRankDto
        {
            Position = await ranked.CountAsync(r => r.CurrentScore!.Value > value, cancellationToken) + 1,
            Of = await ranked.CountAsync(cancellationToken),
        };
    }
}
