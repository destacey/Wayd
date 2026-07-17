namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Scoring.Commands;

public sealed record AssignPortfolioScoringModelCommand(Guid PortfolioId, Guid ScoringModelId) : ICommand;

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
    ILogger<AssignPortfolioScoringModelCommandHandler> logger)
    : ICommandHandler<AssignPortfolioScoringModelCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<AssignPortfolioScoringModelCommandHandler> _logger = logger;

    public async Task<Result> Handle(AssignPortfolioScoringModelCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _ppmDbContext.Portfolios
            .FirstOrDefaultAsync(p => p.Id == request.PortfolioId, cancellationToken);
        if (portfolio is null)
        {
            _logger.LogInformation("Project Portfolio {PortfolioId} not found.", request.PortfolioId);
            return Result.Failure("Project Portfolio not found.");
        }

        var model = await _ppmDbContext.ScoringModels
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.ScoringModelId, cancellationToken);
        if (model is null)
        {
            _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.ScoringModelId);
            return Result.Failure("Scoring Model not found.");
        }

        var assignResult = portfolio.AssignScoringModel(model);
        if (assignResult.IsFailure)
        {
            return Result.Failure(assignResult.Error);
        }

        await _ppmDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
