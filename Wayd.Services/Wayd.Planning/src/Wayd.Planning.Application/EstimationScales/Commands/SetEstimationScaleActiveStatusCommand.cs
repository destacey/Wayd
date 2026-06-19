namespace Wayd.Planning.Application.EstimationScales.Commands;

public sealed record SetEstimationScaleActiveStatusCommand(int Id, bool IsActive) : ICommand;

public sealed class SetEstimationScaleActiveStatusCommandValidator : CustomValidator<SetEstimationScaleActiveStatusCommand>
{
    public SetEstimationScaleActiveStatusCommandValidator()
    {
        RuleFor(c => c.Id)
            .GreaterThan(0);
    }
}

internal sealed class SetEstimationScaleActiveStatusCommandHandler(IPlanningDbContext planningDbContext, ILogger<SetEstimationScaleActiveStatusCommandHandler> logger) : ICommandHandler<SetEstimationScaleActiveStatusCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<SetEstimationScaleActiveStatusCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetEstimationScaleActiveStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var scale = await _planningDbContext.EstimationScales
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (scale is null)
                return Result.Failure("Estimation scale not found.");

            if (!request.IsActive)
            {
                var otherActiveCount = await _planningDbContext.EstimationScales
                    .CountAsync(s => s.IsActive && s.Id != request.Id, cancellationToken);

                if (otherActiveCount == 0)
                    return Result.Failure("Cannot deactivate the last active estimation scale. At least one active estimation scale is required.");
            }

            var result = request.IsActive
                ? scale.Activate()
                : scale.Deactivate();

            if (result.IsFailure)
                return result;

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
