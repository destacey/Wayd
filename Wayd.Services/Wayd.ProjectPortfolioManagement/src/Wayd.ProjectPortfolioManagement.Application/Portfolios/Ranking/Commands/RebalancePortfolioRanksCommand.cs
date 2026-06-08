using Wayd.Common.Application.Identity;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Commands;

public sealed record RebalancePortfolioRanksCommand(Guid PortfolioId) : ICommand;

public sealed class RebalancePortfolioRanksCommandValidator : AbstractValidator<RebalancePortfolioRanksCommand>
{
    public RebalancePortfolioRanksCommandValidator()
    {
        RuleFor(x => x.PortfolioId).NotEmpty();
    }
}

internal sealed class RebalancePortfolioRanksCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentUser currentUser,
    ILogger<RebalancePortfolioRanksCommandHandler> logger)
    : ICommandHandler<RebalancePortfolioRanksCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<RebalancePortfolioRanksCommandHandler> _logger = logger;

    public async Task<Result> Handle(RebalancePortfolioRanksCommand request, CancellationToken cancellationToken)
    {
        // A rebalance is either a deliberate human maintenance action (authorized as a portfolio
        // Owner/Manager) or system-initiated housekeeping with no human actor (a scheduled job, which
        // runs as the well-known system identity and carries no employee claim). For the system path
        // we bypass the per-actor check; a normal user still needs an employee id + Owner/Manager.
        var isSystem = SystemIdentity.IsSystem(_currentUser.GetUserId());
        var employeeId = _currentUser.GetEmployeeId();
        if (!isSystem && employeeId is null)
            return Result.Failure("Unable to determine the current user's employee Id.");

        var portfolio = await _ppmDbContext.Portfolios
            .AsSplitQuery()
            .Include(p => p.Roles)
            .Include(p => p.Projects)
            .FirstOrDefaultAsync(p => p.Id == request.PortfolioId, cancellationToken);

        if (portfolio is null)
        {
            _logger.LogInformation("Project Portfolio {PortfolioId} not found.", request.PortfolioId);
            return Result.Failure("Project Portfolio not found.");
        }

        var rebalanceResult = portfolio.RebalanceRanks(employeeId ?? Guid.Empty, bypassManageCheck: isSystem);
        if (rebalanceResult.IsFailure)
        {
            _logger.LogInformation("Unable to rebalance ranks in portfolio {PortfolioId}. Error: {Error}", request.PortfolioId, rebalanceResult.Error);
            return Result.Failure(rebalanceResult.Error);
        }

        await _ppmDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
