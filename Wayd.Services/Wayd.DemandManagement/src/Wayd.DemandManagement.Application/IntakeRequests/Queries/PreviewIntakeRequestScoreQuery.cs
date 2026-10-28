using Wayd.Common.Application.Scoring.ScoringModels.Dtos;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// What the ratings entered so far would score the request, with its desk's model, without recording it.
/// </summary>
/// <remarks>
/// The rating form's live preview. Held to the same rule as scoring, an agent on the request's desk, rather
/// than to the scoring model claim the admin test panel needs, which an agent need not hold.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="Ratings">A rating for every criterion of the desk's model.</param>
public sealed record PreviewIntakeRequestScoreQuery(Guid Id, IReadOnlyList<IntakeRequestCriterionRatingInput> Ratings)
    : IQuery<Result<ScoringModelEvaluationDto>>;

public sealed class PreviewIntakeRequestScoreQueryValidator : CustomValidator<PreviewIntakeRequestScoreQuery>
{
    public PreviewIntakeRequestScoreQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty();
        RuleFor(q => q.Ratings).NotNull();
        RuleForEach(q => q.Ratings).ChildRules(r => r.RuleFor(x => x.CriterionId).NotEmpty());
    }
}

public sealed class PreviewIntakeRequestScoreQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<PreviewIntakeRequestScoreQuery, Result<ScoringModelEvaluationDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<ScoringModelEvaluationDto>> Handle(PreviewIntakeRequestScoreQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var deskId = await _dbContext.IntakeRequests
            .Where(r => r.Id == request.Id)
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks))
            .Select(r => (Guid?)r.DeskId)
            .FirstOrDefaultAsync(cancellationToken);
        if (deskId is null)
            return Result.Failure<ScoringModelEvaluationDto>(IntakeRequestAccess.NotFoundError);

        if (!reader.IsAgentOn(deskId.Value))
            return Result.Failure<ScoringModelEvaluationDto>(IntakeRequestAuthorization.NotAnAgentError);

        var modelId = await _dbContext.IntakeDesks.Where(d => d.Id == deskId).Select(d => d.ScoringModelId).FirstAsync(cancellationToken);
        var model = modelId is { } id ? await _dbContext.FindForScoring(id, cancellationToken) : null;
        if (model is null)
            return Result.Failure<ScoringModelEvaluationDto>(IntakeRequest.DeskDoesNotScoreError);

        var ratings = IntakeRequestScoreRatings.Resolve(model, request.Ratings);
        if (ratings.IsFailure)
            return Result.Failure<ScoringModelEvaluationDto>(ratings.Error);

        var result = model.CalculateScore(ratings.Value.Values);
        if (result.IsFailure)
            return Result.Failure<ScoringModelEvaluationDto>(result.Error);

        return new ScoringModelEvaluationDto
        {
            PrimaryValue = result.Value.PrimaryValue,
            Outputs = [.. model.Outputs.OrderBy(o => o.Order).Select(o => new ScoringModelOutputValueDto
            {
                Token = o.Token,
                Name = o.Name,
                Value = result.Value.OutputValues[o.Token],
                IsPrimary = o.IsPrimary,
                Order = o.Order,
            })],
        };
    }
}
