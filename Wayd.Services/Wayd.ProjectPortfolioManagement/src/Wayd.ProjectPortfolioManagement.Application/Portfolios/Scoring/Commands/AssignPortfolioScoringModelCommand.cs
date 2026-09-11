using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Scoring.Commands;

public sealed record AssignPortfolioScoringModelCommand(Guid PortfolioId, Guid ScoringModelId) : ICommand, IRequireLinkedEmployee;

public sealed class AssignPortfolioScoringModelCommandValidator : AbstractValidator<AssignPortfolioScoringModelCommand>
{
    public AssignPortfolioScoringModelCommandValidator()
    {
        RuleFor(x => x.PortfolioId).NotEmpty();
        RuleFor(x => x.ScoringModelId).NotEmpty();
    }
}

public sealed class AssignPortfolioScoringModelCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    ILogger<AssignPortfolioScoringModelCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AssignPortfolioScoringModelCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<AssignPortfolioScoringModelCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(AssignPortfolioScoringModelCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

        // A portfolio has no ancestor, so its own roles are the whole leadership picture.
        // The assigned model is loaded because the event names the model being replaced.
        var portfolio = await _ppmDbContext.Portfolios
            .Include(p => p.Roles)
            .Include(p => p.ScoringModel)
            .FirstOrDefaultAsync(p => p.Id == request.PortfolioId, cancellationToken);
        if (portfolio is null)
        {
            _logger.LogInformation("Project Portfolio {PortfolioId} not found.", request.PortfolioId);
            return Result.Failure("Project Portfolio not found.");
        }

        // Tracked, because the portfolio holds it as its navigation; an untracked instance there would be
        // inserted as a new model on save.
        var model = await _ppmDbContext.ScoringModels
            .FirstOrDefaultAsync(m => m.Id == request.ScoringModelId, cancellationToken);
        if (model is null)
        {
            _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.ScoringModelId);
            return Result.Failure("Scoring Model not found.");
        }

        var assignResult = portfolio.AssignScoringModel(model, actor, _dateTimeProvider.Now);
        if (assignResult.IsFailure)
        {
            return Result.Failure(assignResult.Error);
        }

        await _ppmDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
