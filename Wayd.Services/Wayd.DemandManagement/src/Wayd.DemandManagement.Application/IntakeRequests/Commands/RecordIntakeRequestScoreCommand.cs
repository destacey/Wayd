using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Scores an intake request with its desk's scoring model, adding a snapshot to its score history.
/// </summary>
/// <remarks>
/// Needs an agent on the request's desk, with no claim beside it, and an open request on a desk that scores.
/// Answers the new score's id.
/// </remarks>
/// <param name="Id">The request.</param>
/// <param name="Ratings">A rating for every criterion of the desk's model.</param>
public sealed record RecordIntakeRequestScoreCommand(Guid Id, IReadOnlyList<IntakeRequestCriterionRatingInput> Ratings)
    : ICommand<Guid>, IRequireLinkedEmployee;

public sealed class RecordIntakeRequestScoreCommandValidator : CustomValidator<RecordIntakeRequestScoreCommand>
{
    public RecordIntakeRequestScoreCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Ratings).NotEmpty();
        RuleForEach(c => c.Ratings).ChildRules(r =>
        {
            r.RuleFor(x => x.CriterionId).NotEmpty();
            r.RuleFor(x => x).Must(x => x.Value is not null || x.RatingLevelId is not null)
                .WithMessage("Each rating needs a value or a level.");
        });
    }
}

public sealed class RecordIntakeRequestScoreCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RecordIntakeRequestScoreCommand, Guid>
{
    /// <summary>The failure for a score saved at the same moment as another agent's.</summary>
    public const string ScoredMeanwhileError = "Someone scored this request at the same moment. Reload it and try again.";

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<Guid>> Handle(RecordIntakeRequestScoreCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var intakeRequest = await _dbContext.FindVisible(reader, request.Id, includeParticipants: false, cancellationToken,
            includeScores: true);
        if (intakeRequest is null)
            return Result.Failure<Guid>(IntakeRequestAccess.NotFoundError);

        if (!reader.IsAgentOn(intakeRequest.DeskId))
            return Result.Failure<Guid>(IntakeRequestAuthorization.NotAnAgentError);

        var desk = await _dbContext.IntakeDesks.AsNoTracking().FirstAsync(d => d.Id == intakeRequest.DeskId, cancellationToken);
        if (desk.ScoringModelId is not { } modelId)
            return Result.Failure<Guid>(IntakeRequest.DeskDoesNotScoreError);

        var model = await _dbContext.FindForScoring(modelId, cancellationToken)
            ?? throw new InvalidOperationException($"Desk {desk.Id} scores with model {modelId}, which does not exist.");

        var ratings = IntakeRequestScoreRatings.Resolve(model, request.Ratings);
        if (ratings.IsFailure)
            return Result.Failure<Guid>(ratings.Error);

        var scorer = new IntakeRequestEditor(reader.EmployeeId, IsDeskAgent: true,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId));
        var result = intakeRequest.RecordScore(desk, model, ratings.Value.Values, ratings.Value.Levels, scorer,
            _dateTimeProvider.Now);
        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The sequence is unique per request, so a failed save is another score taking the same number.
            return Result.Failure<Guid>(ScoredMeanwhileError);
        }

        return result.Value.Id;
    }
}
