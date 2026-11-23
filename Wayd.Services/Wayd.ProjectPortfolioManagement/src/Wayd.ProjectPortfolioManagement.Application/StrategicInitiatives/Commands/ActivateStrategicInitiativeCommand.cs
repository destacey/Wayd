using Wayd.Common.Domain.Events;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;

public sealed record ActivateStrategicInitiativeCommand(Guid Id) : ICommand;

public sealed class ActivateStrategicInitiativeCommandValidator : AbstractValidator<ActivateStrategicInitiativeCommand>
{
    public ActivateStrategicInitiativeCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ActivateStrategicInitiativeCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<ActivateStrategicInitiativeCommandHandler> logger, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider) : ICommandHandler<ActivateStrategicInitiativeCommand>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ActivateStrategicInitiativeCommandHandler> _logger = logger;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ActivateStrategicInitiativeCommand request, CancellationToken cancellationToken)
    {
        var strategicInitiative = await _projectPortfolioManagementDbContext.StrategicInitiatives
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (strategicInitiative is null)
        {
            _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} not found.", request.Id);
            return Result.Failure("Strategic Initiative not found.");
        }

        var activateResult = strategicInitiative.Activate(EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
        if (activateResult.IsFailure)
        {
            strategicInitiative.ClearDomainEvents();

            _logger.LogError("Unable to activate Strategic Initiative {StrategicInitiativeId}.  Error message: {Error}", request.Id, activateResult.Error);

            return Result.Failure(activateResult.Error);
        }

        await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} activated.", request.Id);

        return Result.Success();
    }
}
