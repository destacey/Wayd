using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Scoring.Commands;

public sealed record ClearPortfolioScoringModelCommand(Guid PortfolioId) : ICommand, IRequireLinkedEmployee;

public sealed class ClearPortfolioScoringModelCommandValidator : AbstractValidator<ClearPortfolioScoringModelCommand>
{
    public ClearPortfolioScoringModelCommandValidator()
    {
        RuleFor(x => x.PortfolioId).NotEmpty();
    }
}

public sealed class ClearPortfolioScoringModelCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    ILogger<ClearPortfolioScoringModelCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ClearPortfolioScoringModelCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ClearPortfolioScoringModelCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ClearPortfolioScoringModelCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

        // A portfolio has no ancestor, so its own roles are the whole leadership picture.
        // The assigned model is loaded because the event names the model being cleared.
        var portfolio = await _ppmDbContext.Portfolios
            .Include(p => p.Roles)
            .Include(p => p.ScoringModel)
            .FirstOrDefaultAsync(p => p.Id == request.PortfolioId, cancellationToken);
        if (portfolio is null)
        {
            _logger.LogInformation("Project Portfolio {PortfolioId} not found.", request.PortfolioId);
            return Result.Failure("Project Portfolio not found.");
        }

        var clearResult = portfolio.ClearScoringModel(actor, _dateTimeProvider.Now);
        if (clearResult.IsFailure)
        {
            return Result.Failure(clearResult.Error);
        }

        await _ppmDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
