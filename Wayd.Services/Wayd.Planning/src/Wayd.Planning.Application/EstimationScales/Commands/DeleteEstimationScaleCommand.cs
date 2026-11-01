namespace Wayd.Planning.Application.EstimationScales.Commands;

/// <summary>
/// Delete an estimation scale and its versions. Refused while any poker session uses the scale.
/// </summary>
public sealed record DeleteEstimationScaleCommand(Guid Id) : ICommand;

public sealed class DeleteEstimationScaleCommandValidator : CustomValidator<DeleteEstimationScaleCommand>
{
    public DeleteEstimationScaleCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();
    }
}

public sealed class DeleteEstimationScaleCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<DeleteEstimationScaleCommandHandler> logger) : ICommandHandler<DeleteEstimationScaleCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<DeleteEstimationScaleCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteEstimationScaleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var scale = await _planningDbContext.EstimationScales
                .Include(s => s.Versions)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (scale is null)
                return Result.Failure("Estimation scale not found.");

            var isInUse = await _planningDbContext.PokerSessions
                .AnyAsync(s => s.EstimationScaleId == request.Id, cancellationToken);

            if (isInUse)
                return Result.Failure("Cannot delete an estimation scale that is in use by poker sessions.");

            if (scale.IsActive)
            {
                var otherActiveCount = await _planningDbContext.EstimationScales
                    .CountAsync(s => s.IsActive && s.Id != request.Id, cancellationToken);

                if (otherActiveCount == 0)
                    return Result.Failure("Cannot delete the last active estimation scale. At least one active estimation scale is required.");
            }

            scale.Delete(EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
            _planningDbContext.EstimationScales.Remove(scale);
            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
