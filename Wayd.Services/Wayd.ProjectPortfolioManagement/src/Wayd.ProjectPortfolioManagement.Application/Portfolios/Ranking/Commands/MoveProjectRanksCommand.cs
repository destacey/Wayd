namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Commands;

public sealed record MoveProjectRanksCommand(
    Guid PortfolioId,
    IReadOnlyList<Guid> ProjectIds,
    Guid? AfterProjectId,
    Guid? BeforeProjectId) : ICommand;

public sealed class MoveProjectRanksCommandValidator : AbstractValidator<MoveProjectRanksCommand>
{
    public MoveProjectRanksCommandValidator()
    {
        RuleFor(x => x.PortfolioId).NotEmpty();

        RuleFor(x => x.ProjectIds)
            .NotEmpty()
            .WithMessage("At least one project must be supplied.");

        RuleForEach(x => x.ProjectIds).NotEmpty();

        RuleFor(x => x.ProjectIds)
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("The batch contains duplicate projects.");

        RuleFor(x => x)
            .Must(x => x.AfterProjectId is not null || x.BeforeProjectId is not null)
            .WithMessage("At least one anchor must be supplied.");

        RuleFor(x => x)
            .Must(x => x.AfterProjectId is null || x.ProjectIds is null || !x.ProjectIds.Contains(x.AfterProjectId.Value))
            .WithMessage("An anchor cannot also be in the batch.");

        RuleFor(x => x)
            .Must(x => x.BeforeProjectId is null || x.ProjectIds is null || !x.ProjectIds.Contains(x.BeforeProjectId.Value))
            .WithMessage("An anchor cannot also be in the batch.");
    }
}

internal sealed class MoveProjectRanksCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentUser currentUser,
    ILogger<MoveProjectRanksCommandHandler> logger)
    : ICommandHandler<MoveProjectRanksCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<MoveProjectRanksCommandHandler> _logger = logger;

    public async Task<Result> Handle(MoveProjectRanksCommand request, CancellationToken cancellationToken)
    {
        var employeeId = _currentUser.GetEmployeeId();
        if (employeeId is null)
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

        var moveResult = portfolio.MoveProjectRanks(
            employeeId.Value,
            request.ProjectIds,
            request.AfterProjectId,
            request.BeforeProjectId);

        if (moveResult.IsFailure)
        {
            _logger.LogInformation("Unable to rank projects in portfolio {PortfolioId}. Error: {Error}", request.PortfolioId, moveResult.Error);
            return Result.Failure(moveResult.Error);
        }

        await _ppmDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
