namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Command;

public sealed record DeleteProjectPortfolioCommand(Guid Id) : ICommand;

public sealed class DeleteProjectPortfolioCommandValidator : AbstractValidator<DeleteProjectPortfolioCommand>
{
    public DeleteProjectPortfolioCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class DeleteProjectPortfolioCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<DeleteProjectPortfolioCommandHandler> logger) : ICommandHandler<DeleteProjectPortfolioCommand>
{
    private const string AppRequestName = nameof(DeleteProjectPortfolioCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<DeleteProjectPortfolioCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteProjectPortfolioCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var portfolio = await _projectPortfolioManagementDbContext.Portfolios
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (portfolio is null)
            {
                _logger.LogInformation("Project Portfolio {ProjectPortfolioId} not found.", request.Id);
                return Result.Failure("Project Portfolio not found.");
            }

            if (!portfolio.CanBeDeleted())
            {
                _logger.LogInformation("Project Portfolio {ProjectPortfolioId} cannot be deleted.", request.Id);
                return Result.Failure("Project Portfolio cannot be deleted.");
            }

            _projectPortfolioManagementDbContext.Portfolios.Remove(portfolio);
            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project Portfolio {ProjectPortfolioId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
