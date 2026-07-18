using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.Visions.Commands;

public sealed record ActivateVisionCommand(Guid Id) : ICommand;

public sealed class ActivateVisionCommandValidator : AbstractValidator<ActivateVisionCommand>
{
    public ActivateVisionCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ActivateVisionCommandHandler(IStrategicManagementDbContext strategicManagementDbContext, ILogger<ActivateVisionCommandHandler> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<ActivateVisionCommand>
{
    private const string AppRequestName = nameof(ActivateVisionCommand);

    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ILogger<ActivateVisionCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ActivateVisionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var visions = await _strategicManagementDbContext.Visions
                .ToListAsync(cancellationToken);
            if (visions.Count == 0)
            {
                _logger.LogInformation("No Visions found.");
                return Result.Failure("No Visions found.");
            }

            var vision = visions.SingleOrDefault(v => v.Id == request.Id);
            if (vision is null)
            {
                _logger.LogInformation("Vision {VisionId} not found.", request.Id);
                return Result.Failure("Vision not found.");
            }

            var aggregate = new VisionAggregate(visions);

            var activateResult = aggregate.Activate(request.Id, _dateTimeProvider.Now);
            if (activateResult.IsFailure)
            {
                // Reset the entity
                await _strategicManagementDbContext.Entry(vision).ReloadAsync(cancellationToken);
                vision.ClearDomainEvents();

                _logger.LogError("Unable to activate Vision {VisionId}.  Error message: {Error}", request.Id, activateResult.Error);
                return Result.Failure(activateResult.Error);
            }
            await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Vision {VisionId} activated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}