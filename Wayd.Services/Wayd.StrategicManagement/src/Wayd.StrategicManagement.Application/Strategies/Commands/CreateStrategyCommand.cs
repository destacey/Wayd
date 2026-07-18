using Wayd.Common.Application.Models;
using Wayd.StrategicManagement.Domain.Enums;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.Strategies.Commands;

public sealed record CreateStrategyCommand(string Name, string Description, StrategyStatus Status, FlexibleDateRange? Dates) : ICommand<ObjectIdAndKey>;

public sealed class CreateStrategyCommandValidator : AbstractValidator<CreateStrategyCommand>
{
    public CreateStrategyCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(x => x.Description)
            .MaximumLength(3072);

        RuleFor(x => x.Status)
            .IsInEnum();
    }
}

public sealed class CreateStrategyCommandHandler(IStrategicManagementDbContext strategicManagementDbContext, ILogger<CreateStrategyCommandHandler> logger) : ICommandHandler<CreateStrategyCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateStrategyCommand);

    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ILogger<CreateStrategyCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateStrategyCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategy = Strategy.Create(
                request.Name,
                request.Description,
                request.Status,
                request.Dates
                );

            await _strategicManagementDbContext.Strategies.AddAsync(strategy, cancellationToken);
            await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategy {StrategyId} created with Key {StrategyKey}.", strategy.Id, strategy.Key);

            return new ObjectIdAndKey(strategy.Id, strategy.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}

