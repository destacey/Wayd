using Wayd.Common.Application.Models;

namespace Wayd.StrategicManagement.Application.Strategies.Commands;

public sealed record DeleteStrategyCommand(Guid Id) : ICommand;
public sealed class DeleteStrategyCommandValidator : AbstractValidator<DeleteStrategyCommand>
{
    public DeleteStrategyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class DeleteStrategyCommandHandler(IStrategicManagementDbContext strategicManagementDbContext, ILogger<DeleteStrategyCommandHandler> logger) : ICommandHandler<DeleteStrategyCommand>
{
    private const string AppRequestName = nameof(DeleteStrategyCommand);

    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ILogger<DeleteStrategyCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteStrategyCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategy = await _strategicManagementDbContext.Strategies
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (strategy is null)
            {
                _logger.LogInformation("Strategy {StrategyId} not found.", request.Id);
                return Result.Failure("Strategy not found.");
            }

            _strategicManagementDbContext.Strategies.Remove(strategy);
            await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategy {StrategyId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
