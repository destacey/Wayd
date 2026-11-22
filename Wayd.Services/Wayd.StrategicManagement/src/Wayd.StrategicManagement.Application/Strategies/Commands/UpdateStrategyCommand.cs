using Wayd.StrategicManagement.Domain.Enums;

namespace Wayd.StrategicManagement.Application.Strategies.Commands;

public sealed record UpdateStrategyCommand(Guid Id, string Name, string Description, StrategyStatus Status, FlexibleDateRange? Dates) : ICommand;

public sealed class UpdateStrategyCommandValidator : AbstractValidator<UpdateStrategyCommand>
{
    public UpdateStrategyCommandValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(x => x.Description)
            .MaximumLength(3072);

        RuleFor(x => x.Status)
            .IsInEnum();
    }
}

public sealed class UpdateStrategyCommandHandler(IStrategicManagementDbContext strategicManagementDbContext, ILogger<UpdateStrategyCommandHandler> logger) : ICommandHandler<UpdateStrategyCommand>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ILogger<UpdateStrategyCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateStrategyCommand request, CancellationToken cancellationToken)
    {
        var strategy = await _strategicManagementDbContext.Strategies
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (strategy is null)
        {
            _logger.LogInformation("Strategy {StrategyId} not found.", request.Id);
            return Result.Failure("Strategy not found.");
        }

        var updateResult = strategy.Update(
            request.Name,
            request.Description,
            request.Status,
            request.Dates
            );

        if (updateResult.IsFailure)
        {
            _logger.LogInformation("Unable to update Strategy {StrategyId}.  Error message: {Error}", request.Id, updateResult.Error);
            return Result.Failure(updateResult.Error);
        }

        await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Strategy {StrategyId} updated.", strategy.Id);

        return Result.Success();
    }
}
