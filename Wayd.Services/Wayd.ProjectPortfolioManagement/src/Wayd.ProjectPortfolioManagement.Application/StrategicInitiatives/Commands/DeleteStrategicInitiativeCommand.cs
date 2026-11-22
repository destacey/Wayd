using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Events;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;

public sealed record DeleteStrategicInitiativeCommand(Guid Id) : ICommand;

public sealed class DeleteStrategicInitiativeCommandValidator : AbstractValidator<DeleteStrategicInitiativeCommand>
{
    public DeleteStrategicInitiativeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class DeleteStrategicInitiativeCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ICurrentUser currentUser,
    ILogger<DeleteStrategicInitiativeCommandHandler> logger,
    IDateTimeProvider dateTimeProvider,
    ILinkWriter linkWriter,
    IKpiWriter kpiWriter)
    : ICommandHandler<DeleteStrategicInitiativeCommand>
{
    private readonly ILinkWriter _linkWriter = linkWriter;
    private readonly IKpiWriter _kpiWriter = kpiWriter;
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<DeleteStrategicInitiativeCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteStrategicInitiativeCommand request, CancellationToken cancellationToken)
    {
        var strategicInitiative = await _projectPortfolioManagementDbContext.StrategicInitiatives
            .Include(s => s.Roles)
            .Include(s => s.StrategicInitiativeProjects)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (strategicInitiative == null)
        {
            _logger.LogInformation("Strategic Initiative with Id {StrategicInitiativeId} not found.", request.Id);
            return Result.Failure("Strategic Initiative not found.");
        }

        var portfolio = await _projectPortfolioManagementDbContext.Portfolios
                .Include(p => p.StrategicInitiatives.Where(i => i.Id == request.Id))
                // The rest of the strategic initiative relationships are already include from the initial strategicInitiative query
                .FirstOrDefaultAsync(p => p.Id == strategicInitiative.PortfolioId, cancellationToken);
        if (portfolio == null)
        {
            _logger.LogInformation("Portfolio with Id {PortfolioId} not found.", strategicInitiative.PortfolioId);
            return Result.Failure("Portfolio not found.");
        }

        var actor = EventActor.User(_currentUser.GetUserId());
        var deleteResult = portfolio.DeleteStrategicInitiative(
            strategicInitiative.Id,
            actor,
            _dateTimeProvider.Now);
        if (deleteResult.IsFailure)
        {
            _logger.LogError("Error deleting Strategic Initiative {StrategicInitiativeId}. Error message: {Error}", request.Id, deleteResult.Error);
            return Result.Failure(deleteResult.Error);
        }

        // A link has no foreign key to its record, so it would outlive the initiative.
        await _linkWriter.RemoveAll(PpmLinkOwners.StrategicInitiative.Key, strategicInitiative.Id, actor, cancellationToken);
        await _kpiWriter.RemoveAll(PpmKpiSubjects.StrategicInitiative.Key, strategicInitiative.Id, actor, cancellationToken);

        await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} with Key {StrategicInitiativeKey} deleted", strategicInitiative.Id, strategicInitiative.Key);

        return Result.Success();
    }
}
