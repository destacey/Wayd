using Wayd.Common.Domain.Events;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;

public sealed record ApproveStrategicInitiativeCommand(Guid Id) : ICommand;

public sealed class ApproveStrategicInitiativeCommandValidator : AbstractValidator<ApproveStrategicInitiativeCommand>
{
    public ApproveStrategicInitiativeCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ApproveStrategicInitiativeCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<ApproveStrategicInitiativeCommandHandler> logger, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider) : ICommandHandler<ApproveStrategicInitiativeCommand>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ApproveStrategicInitiativeCommandHandler> _logger = logger;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ApproveStrategicInitiativeCommand request, CancellationToken cancellationToken)
    {
        var strategicInitiative = await _projectPortfolioManagementDbContext.StrategicInitiatives
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (strategicInitiative is null)
        {
            _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} not found.", request.Id);
            return Result.Failure("Strategic Initiative not found.");
        }

        var approveResult = strategicInitiative.Approve(EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
        if (approveResult.IsFailure)
        {
            strategicInitiative.ClearDomainEvents();

            _logger.LogError("Unable to approve strategic initiative {StrategicInitiativeId}.  Error message: {Error}", request.Id, approveResult.Error);

            return Result.Failure(approveResult.Error);
        }

        await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} approved.", request.Id);

        return Result.Success();
    }
}
