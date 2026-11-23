using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;

public sealed record CloseProjectPortfolioCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

public sealed class CloseProjectPortfolioCommandValidator : AbstractValidator<CloseProjectPortfolioCommand>
{
    public CloseProjectPortfolioCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class CloseProjectPortfolioCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    ILogger<CloseProjectPortfolioCommandHandler> logger,
    IDateTimeProvider dateTimeProvider,
    ISystemCalendar systemCalendar) : ICommandHandler<CloseProjectPortfolioCommand>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<CloseProjectPortfolioCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ISystemCalendar _systemCalendar = systemCalendar;

    public async Task<Result> Handle(CloseProjectPortfolioCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

        var portfolio = await _projectPortfolioManagementDbContext.Portfolios
            .AsSplitQuery()
            .Include(p => p.Roles)
            // Close refuses while a program or project is open; unloaded, the children read as none.
            .Include(p => p.Programs)
            .Include(p => p.Projects)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (portfolio is null)
        {
            _logger.LogInformation("Project Portfolio {ProjectPortfolioId} not found.", request.Id);
            return Result.Failure("Project Portfolio not found.");
        }

        var now = _dateTimeProvider.Now;
        var closeResult = portfolio.Close(actor, await _systemCalendar.DayOf(now, cancellationToken), now);
        if (closeResult.IsFailure)
        {
            _logger.LogError("Unable to close Project Portfolio {ProjectPortfolioId}.  Error message: {Error}", request.Id, closeResult.Error);
            return Result.Failure(closeResult.Error);
        }
        await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Project Portfolio {ProjectPortfolioId} closed.", request.Id);

        return Result.Success();
    }
}