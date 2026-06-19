using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Scoring.ScoringModels.Dtos;

namespace Wayd.Common.Application.Scoring.ScoringModels.Queries;

/// <summary>
/// Evaluates a scoring model against a set of supplied criterion values, returning every output value
/// and the primary score. Used by the model's "test" panel to preview formula results without persisting
/// anything. Each entry pairs a criterion id with the raw value to plug in for that criterion's token.
/// </summary>
public sealed record EvaluateScoringModelQuery(Guid Id, IReadOnlyList<EvaluateScoringModelQuery.CriterionValue> CriterionValues)
    : IQuery<Result<ScoringModelEvaluationDto>>
{
    public sealed record CriterionValue(Guid CriterionId, decimal Value);
}

public sealed class EvaluateScoringModelQueryValidator : AbstractValidator<EvaluateScoringModelQuery>
{
    public EvaluateScoringModelQueryValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.CriterionValues).NotNull();
        RuleForEach(v => v.CriterionValues).ChildRules(cv =>
            cv.RuleFor(c => c.CriterionId).NotEmpty());
    }
}

internal sealed class EvaluateScoringModelQueryHandler(
    IWaydDbContext waydDbContext,
    ILogger<EvaluateScoringModelQueryHandler> logger)
    : IQueryHandler<EvaluateScoringModelQuery, Result<ScoringModelEvaluationDto>>
{
    private const string AppRequestName = nameof(EvaluateScoringModelQuery);
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<EvaluateScoringModelQueryHandler> _logger = logger;

    public async Task<Result<ScoringModelEvaluationDto>> Handle(EvaluateScoringModelQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Raw token values are supplied, so scales/levels aren't needed — only criteria and outputs.
            var model = await _waydDbContext.ScoringModels
                .Include(x => x.Criteria)
                .Include(x => x.Outputs)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (model is null)
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.Id);
                return Result.Failure<ScoringModelEvaluationDto>("Scoring Model not found.");
            }

            var ratingValuesByCriterionId = request.CriterionValues
                .GroupBy(cv => cv.CriterionId)
                .ToDictionary(g => g.Key, g => g.Last().Value);

            var result = model.CalculateScore(ratingValuesByCriterionId);
            if (result.IsFailure)
            {
                return Result.Failure<ScoringModelEvaluationDto>(result.Error);
            }

            var outputs = model.Outputs
                .OrderBy(o => o.Order)
                .Select(o => new ScoringModelOutputValueDto
                {
                    Token = o.Token,
                    Name = o.Name,
                    Value = result.Value.OutputValues[o.Token],
                    IsPrimary = o.IsPrimary,
                    Order = o.Order,
                })
                .ToList();

            return Result.Success(new ScoringModelEvaluationDto
            {
                PrimaryValue = result.Value.PrimaryValue,
                Outputs = outputs,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {QueryName} query for request {@Request}.", AppRequestName, request);
            return Result.Failure<ScoringModelEvaluationDto>($"Error handling {AppRequestName} query.");
        }
    }
}
