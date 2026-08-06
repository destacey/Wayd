namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Commands;

public sealed record RebalancePortfolioRanksCommand(Guid PortfolioId) : ICommand, IRequireLinkedEmployee;

public sealed class RebalancePortfolioRanksCommandValidator : AbstractValidator<RebalancePortfolioRanksCommand>
{
    public RebalancePortfolioRanksCommandValidator()
    {
        RuleFor(x => x.PortfolioId).NotEmpty();
    }
}

public sealed class RebalancePortfolioRanksCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<RebalancePortfolioRanksCommandHandler> logger)
    : ICommandHandler<RebalancePortfolioRanksCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    // Both: ICurrentUser answers "what kind of actor is this?" (the system path below), while
    // ICurrentPrincipal resolves the employee link from the database rather than the token claim.
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<RebalancePortfolioRanksCommandHandler> _logger = logger;

    public async Task<Result> Handle(RebalancePortfolioRanksCommand request, CancellationToken cancellationToken)
    {
        // A rebalance is either a deliberate human maintenance action (authorized as a portfolio
        // Owner/Manager) or system-initiated housekeeping with no human actor (a scheduled job, which
        // runs as ActorKind.System and carries no employee claim). For the system path we bypass the
        // per-actor check; a normal user still needs an employee id + Owner/Manager.
        var isSystem = _currentUser.Kind == ActorKind.System;
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (!isSystem && employeeId is null)
            LinkedEmployeeRequired.Throw();

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
