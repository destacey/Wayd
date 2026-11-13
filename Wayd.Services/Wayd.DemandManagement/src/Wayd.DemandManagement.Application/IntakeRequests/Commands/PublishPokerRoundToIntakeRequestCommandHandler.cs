using Wayd.Common.Application.Requests.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.DemandEstimationScales;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Commands;

/// <summary>
/// Checks the round's session, desk, version and consensus are named before the handler loads anything.
/// </summary>
public sealed class PublishPokerRoundToIntakeRequestCommandValidator : CustomValidator<PublishPokerRoundToIntakeRequestCommand>
{
    public PublishPokerRoundToIntakeRequestCommandValidator()
    {
        RuleFor(c => c.IntakeRequestId).NotEmpty();
        RuleFor(c => c.IntakeDeskId).NotEmpty();
        RuleFor(c => c.EstimationScaleVersionId).NotEmpty();
        RuleFor(c => c.Value).NotEmpty().MaximumLength(32);
        RuleFor(c => c.PokerSessionId).NotEmpty();
        RuleFor(c => c.PokerRoundId).NotEmpty();
    }
}

/// <summary>
/// Decides again, as the publishing facilitator, whether a poker round's consensus may be written to its request,
/// and lets the request write it.
/// </summary>
/// <remarks>
/// Nothing the session held when the round was added is trusted: the request may have closed or moved desk, the
/// facilitator may have left the desk, and the desk may estimate on another scale. Each refusal is a sentence the
/// round shows the facilitator.
/// </remarks>
public sealed class PublishPokerRoundToIntakeRequestCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PublishPokerRoundToIntakeRequestCommand>
{
    /// <summary>
    /// The refusal for a request that is gone or that the facilitator can no longer see, which read the same so a
    /// private desk's request is not confirmed to exist.
    /// </summary>
    public const string RequestUnavailableError = "The request no longer exists, or you can no longer see it.";

    /// <summary>The refusal for a request that has moved off the desk hosting the session.</summary>
    public const string RequestLeftDeskError = "The request has moved to another desk.";

    /// <summary>The refusal for a facilitator who is not, or is no longer, an agent on the request's desk.</summary>
    public const string NotAnAgentError = "Only an agent on the request's desk can publish its estimate.";

    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(PublishPokerRoundToIntakeRequestCommand request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        if (reader.EmployeeId is null)
            LinkedEmployeeRequired.Throw();

        var intakeRequest = await _dbContext.FindVisible(reader, request.IntakeRequestId, includeParticipants: false, cancellationToken);
        if (intakeRequest is null)
            return Result.Failure(RequestUnavailableError);

        if (intakeRequest.DeskId != request.IntakeDeskId)
            return Result.Failure(RequestLeftDeskError);

        if (!reader.IsAgentOn(intakeRequest.DeskId))
            return Result.Failure(NotAnAgentError);

        var desk = await _dbContext.IntakeDesks.AsNoTracking().FirstAsync(d => d.Id == intakeRequest.DeskId, cancellationToken);
        var scale = await _dbContext.FindPinnedScale(desk, cancellationToken);

        var editor = new IntakeRequestEditor(reader.EmployeeId, IsDeskAgent: true,
            EventActor.User(_currentUser.GetUserId(), reader.EmployeeId));
        var result = intakeRequest.PublishPokerRound(request.Value, desk, scale, request.EstimationScaleVersionId,
            new IntakeRequestEstimatePokerRound(request.PokerSessionId, request.PokerRoundId), editor, _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
