namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Scoring.Commands;

public sealed record ClearPortfolioScoringModelCommand(Guid PortfolioId) : ICommand;

public sealed class ClearPortfolioScoringModelCommandValidator : AbstractValidator<ClearPortfolioScoringModelCommand>
{
    public ClearPortfolioScoringModelCommandValidator()
    {
        RuleFor(x => x.PortfolioId).NotEmpty();
    }
}

public sealed class ClearPortfolioScoringModelCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ILogger<ClearPortfolioScoringModelCommandHandler> logger)
    : ICommandHandler<ClearPortfolioScoringModelCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<ClearPortfolioScoringModelCommandHandler> _logger = logger;

    public async Task<Result> Handle(ClearPortfolioScoringModelCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _ppmDbContext.Portfolios
            .FirstOrDefaultAsync(p => p.Id == request.PortfolioId, cancellationToken);
        if (portfolio is null)
        {
            _logger.LogInformation("Project Portfolio {PortfolioId} not found.", request.PortfolioId);
            return Result.Failure("Project Portfolio not found.");
        }

        var clearResult = portfolio.ClearScoringModel();
        if (clearResult.IsFailure)
        {
            return Result.Failure(clearResult.Error);
        }

        await _ppmDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
