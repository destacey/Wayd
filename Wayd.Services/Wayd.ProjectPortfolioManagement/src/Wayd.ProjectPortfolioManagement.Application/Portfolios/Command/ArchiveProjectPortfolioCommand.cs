using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;

public sealed record ArchiveProjectPortfolioCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

public sealed class ArchiveProjectPortfolioCommandValidator : AbstractValidator<ArchiveProjectPortfolioCommand>
{
    public ArchiveProjectPortfolioCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ArchiveProjectPortfolioCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    ILogger<ArchiveProjectPortfolioCommandHandler> logger,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ArchiveProjectPortfolioCommand>
{
    private const string AppRequestName = nameof(ArchiveProjectPortfolioCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ArchiveProjectPortfolioCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ArchiveProjectPortfolioCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

            var portfolio = await _projectPortfolioManagementDbContext.Portfolios
                .Include(p => p.Roles)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (portfolio is null)
            {
                _logger.LogInformation("Project Portfolio {ProjectPortfolioId} not found.", request.Id);
                return Result.Failure("Project Portfolio not found.");
            }

            var archiveResult = portfolio.Archive(actor);
            if (archiveResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(portfolio).ReloadAsync(cancellationToken);
                portfolio.ClearDomainEvents();

                _logger.LogError("Unable to archive Project Portfolio {ProjectPortfolioId}.  Error message: {Error}", request.Id, archiveResult.Error);
                return Result.Failure(archiveResult.Error);
            }
            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project Portfolio {ProjectPortfolioId} archived.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
