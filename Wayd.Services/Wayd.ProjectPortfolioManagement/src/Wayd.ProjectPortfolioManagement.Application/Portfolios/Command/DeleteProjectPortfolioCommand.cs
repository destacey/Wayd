using Wayd.Common.Domain.Events;

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

public sealed class DeleteProjectPortfolioCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ICurrentUser currentUser, ILogger<DeleteProjectPortfolioCommandHandler> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<DeleteProjectPortfolioCommand>
{
    private const string AppRequestName = nameof(DeleteProjectPortfolioCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<DeleteProjectPortfolioCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

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

            var deleteResult = portfolio.Delete(EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
            if (deleteResult.IsFailure)
            {
                _logger.LogInformation("Project Portfolio {ProjectPortfolioId} cannot be deleted. Error message: {Error}", request.Id, deleteResult.Error);
                return Result.Failure(deleteResult.Error);
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
