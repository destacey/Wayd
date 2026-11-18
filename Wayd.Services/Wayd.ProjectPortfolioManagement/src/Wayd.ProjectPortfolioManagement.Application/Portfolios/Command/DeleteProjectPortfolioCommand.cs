using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Events;
using Wayd.ProjectPortfolioManagement.Domain;

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

public sealed class DeleteProjectPortfolioCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ICurrentUser currentUser, ILogger<DeleteProjectPortfolioCommandHandler> logger, IDateTimeProvider dateTimeProvider, ILinkWriter linkWriter) : ICommandHandler<DeleteProjectPortfolioCommand>
{
    private const string AppRequestName = nameof(DeleteProjectPortfolioCommand);

    private readonly ILinkWriter _linkWriter = linkWriter;

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

            var actor = EventActor.User(_currentUser.GetUserId());
            var deleteResult = portfolio.Delete(actor, _dateTimeProvider.Now);
            if (deleteResult.IsFailure)
            {
                _logger.LogInformation("Project Portfolio {ProjectPortfolioId} cannot be deleted. Error message: {Error}", request.Id, deleteResult.Error);
                return Result.Failure(deleteResult.Error);
            }

            _projectPortfolioManagementDbContext.Portfolios.Remove(portfolio);

            // A link has no foreign key to its record, so it would outlive the portfolio.
            await _linkWriter.RemoveAll(PpmLinkOwners.Portfolio.Key, portfolio.Id, actor, cancellationToken);
            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project Portfolio {ProjectPortfolioId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command.", AppRequestName);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
