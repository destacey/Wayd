using Wayd.Common.Domain.Scoring;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Commands;

public sealed record RecordProjectScoreCommand(
    Guid ProjectId,
    IReadOnlyList<RecordProjectScoreCommand.CriterionRatingInput> Ratings) : ICommand<Guid>
{
    /// <summary>
    /// A single criterion's rating. For scale-based criteria, supply <see cref="RatingLevelId"/>; the
    /// numeric value is taken from the level. For free-numeric criteria, supply <see cref="Value"/>.
    /// </summary>
    public sealed record CriterionRatingInput(Guid CriterionId, decimal? Value, Guid? RatingLevelId);
}

public sealed class RecordProjectScoreCommandValidator : AbstractValidator<RecordProjectScoreCommand>
{
    public RecordProjectScoreCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();

        RuleFor(x => x.Ratings)
            .NotEmpty()
            .WithMessage("At least one criterion rating is required.");

        RuleForEach(x => x.Ratings).ChildRules(rating =>
        {
            rating.RuleFor(r => r.CriterionId).NotEmpty();
            rating.RuleFor(r => r)
                .Must(r => r.RatingLevelId is not null || r.Value is not null)
                .WithMessage("Each criterion must have either a selected rating level or a numeric value.");
        });
    }
}

internal sealed class RecordProjectScoreCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<RecordProjectScoreCommandHandler> logger)
    : ICommandHandler<RecordProjectScoreCommand, Guid>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<RecordProjectScoreCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(RecordProjectScoreCommand request, CancellationToken cancellationToken)
    {
        var employeeId = _currentUser.GetEmployeeId();
        if (employeeId is null)
            return Result.Failure<Guid>("Unable to determine the current user's employee Id.");

        var project = await _ppmDbContext.Projects
            .AsSplitQuery()
            .Include(p => p.Roles)
            .Include(p => p.Scores)
            .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
            .Include(p => p.Program).ThenInclude(p => p!.Roles)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (project is null)
        {
            _logger.LogInformation("Project {ProjectId} not found.", request.ProjectId);
            return Result.Failure<Guid>($"Project {request.ProjectId} not found.");
        }

        var modelId = project.Portfolio!.ScoringModelId;
        if (modelId is null)
            return Result.Failure<Guid>("Scoring is not enabled for this project's portfolio.");

        var model = await _ppmDbContext.ScoringModels
            .AsNoTracking()
            .AsSplitQuery()
            .Include(m => m.Criteria)
            .Include(m => m.Scales).ThenInclude(s => s.Levels)
            .Include(m => m.Outputs)
            .FirstOrDefaultAsync(m => m.Id == modelId.Value, cancellationToken);

        if (model is null)
            return Result.Failure<Guid>("The portfolio's assigned scoring model could not be found.");

        var resolveResult = ResolveRatings(model, request.Ratings);
        if (resolveResult.IsFailure)
            return Result.Failure<Guid>(resolveResult.Error);

        var (ratingValues, selectedLevels) = resolveResult.Value;

        var recordResult = project.RecordScore(
            model,
            ratingValues,
            selectedLevels,
            employeeId.Value,
            project.Portfolio!.Roles,
            project.Program?.Roles,
            _dateTimeProvider.Now);

        if (recordResult.IsFailure)
        {
            _logger.LogInformation("Unable to record score for project {ProjectId}. Error: {Error}", request.ProjectId, recordResult.Error);
            return Result.Failure<Guid>(recordResult.Error);
        }

        await _ppmDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(recordResult.Value.Id);
    }

    private static Result<(IReadOnlyDictionary<Guid, decimal> Values, IReadOnlyDictionary<Guid, (Guid LevelId, string Label)> Levels)> ResolveRatings(
        ScoringModel model,
        IReadOnlyList<RecordProjectScoreCommand.CriterionRatingInput> ratings)
    {
        var ratingsByCriterionId = ratings.ToDictionary(r => r.CriterionId);
        var values = new Dictionary<Guid, decimal>();
        var levels = new Dictionary<Guid, (Guid LevelId, string Label)>();

        foreach (var criterion in model.Criteria)
        {
            if (!ratingsByCriterionId.TryGetValue(criterion.Id, out var rating))
                return Result.Failure<(IReadOnlyDictionary<Guid, decimal>, IReadOnlyDictionary<Guid, (Guid, string)>)>(
                    $"Criterion '{criterion.Name}' has not been rated.");

            if (criterion.ScaleId is not null)
            {
                if (rating.RatingLevelId is null)
                    return Result.Failure<(IReadOnlyDictionary<Guid, decimal>, IReadOnlyDictionary<Guid, (Guid, string)>)>(
                        $"Criterion '{criterion.Name}' must be rated by selecting a level from its scale.");

                var scale = model.Scales.FirstOrDefault(s => s.Id == criterion.ScaleId.Value);
                var level = scale?.Levels.FirstOrDefault(l => l.Id == rating.RatingLevelId.Value);
                if (level is null)
                    return Result.Failure<(IReadOnlyDictionary<Guid, decimal>, IReadOnlyDictionary<Guid, (Guid, string)>)>(
                        $"The selected rating level for criterion '{criterion.Name}' does not belong to its scale.");

                values[criterion.Id] = level.Value;
                levels[criterion.Id] = (level.Id, level.Label);
            }
            else
            {
                if (rating.Value is null)
                    return Result.Failure<(IReadOnlyDictionary<Guid, decimal>, IReadOnlyDictionary<Guid, (Guid, string)>)>(
                        $"Criterion '{criterion.Name}' must be rated with a numeric value.");

                values[criterion.Id] = rating.Value.Value;
            }
        }

        return Result.Success<(IReadOnlyDictionary<Guid, decimal>, IReadOnlyDictionary<Guid, (Guid, string)>)>((values, levels));
    }
}
