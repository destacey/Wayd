namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;

public sealed record CloseProjectPortfolioCommand(Guid Id) : ICommand;

public sealed class CloseProjectPortfolioCommandValidator : AbstractValidator<CloseProjectPortfolioCommand>
{
    public CloseProjectPortfolioCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class CloseProjectPortfolioCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<CloseProjectPortfolioCommandHandler> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<CloseProjectPortfolioCommand>
{
    private const string AppRequestName = nameof(CloseProjectPortfolioCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<CloseProjectPortfolioCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(CloseProjectPortfolioCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var portfolio = await _projectPortfolioManagementDbContext.Portfolios
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (portfolio is null)
            {
                _logger.LogInformation("Project Portfolio {ProjectPortfolioId} not found.", request.Id);
                return Result.Failure("Project Portfolio not found.");
            }

            var closeResult = portfolio.Close(_dateTimeProvider.Today);
            if (closeResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(portfolio).ReloadAsync(cancellationToken);
                portfolio.ClearDomainEvents();

                _logger.LogError("Unable to close Project Portfolio {ProjectPortfolioId}.  Error message: {Error}", request.Id, closeResult.Error);
                return Result.Failure(closeResult.Error);
            }
            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project Portfolio {ProjectPortfolioId} closed.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}