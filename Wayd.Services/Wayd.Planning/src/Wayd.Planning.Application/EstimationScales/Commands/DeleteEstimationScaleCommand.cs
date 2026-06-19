namespace Wayd.Planning.Application.EstimationScales.Commands;

public sealed record DeleteEstimationScaleCommand(int Id) : ICommand;

public sealed class DeleteEstimationScaleCommandValidator : CustomValidator<DeleteEstimationScaleCommand>
{
    public DeleteEstimationScaleCommandValidator()
    {
        RuleFor(c => c.Id)
            .GreaterThan(0);
    }
}

internal sealed class DeleteEstimationScaleCommandHandler(IPlanningDbContext planningDbContext, ILogger<DeleteEstimationScaleCommandHandler> logger) : ICommandHandler<DeleteEstimationScaleCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<DeleteEstimationScaleCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteEstimationScaleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var scale = await _planningDbContext.EstimationScales
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
