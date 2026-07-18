namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record DeleteProjectCommand(Guid Id) : ICommand;

public sealed class DeleteProjectCommandValidator : AbstractValidator<DeleteProjectCommand>
{
    public DeleteProjectCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class DeleteProjectCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<DeleteProjectCommandHandler> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<DeleteProjectCommand>
{
    private const string AppRequestName = nameof(DeleteProjectCommand);
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<DeleteProjectCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var project = await _projectPortfolioManagementDbContext.Projects
                .Include(p => p.Roles)
                .Include(p => p.StrategicThemeTags)
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found.", request.Id);
                return Result.Failure("Project not found.");
            }

            var portfolioQuery = _projectPortfolioManagementDbContext.Portfolios
                    .Include(p => p.Projects.Where(p => p.Id == request.Id))
                    // The rest of the project relationships are already include from the initial project query
                    .AsQueryable();
            if (project.ProgramId.HasValue)
            {
                portfolioQuery = portfolioQuery
                    .Include(p => p.Programs.Where(p => p.Id == project.ProgramId));
            }

            var portfolio = await portfolioQuery
                    .FirstOrDefaultAsync(p => p.Id == project.PortfolioId, cancellationToken);
            if (portfolio == null)
            {
                _logger.LogInformation("Portfolio with Id {PortfolioId} not found.", project.PortfolioId);
                return Result.Failure("Portfolio not found.");
            }

            var deleteResult = portfolio.DeleteProject(project.Id, _dateTimeProvider.Now);
            if (deleteResult.IsFailure)
            {
                _logger.LogError("Error deleting project {ProjectId}. Error message: {Error}", request.Id, deleteResult.Error);
                return Result.Failure(deleteResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project {ProjectId} deleted. Key: {ProjectKey}, Name: {ProjectName}", project.Id, project.Key.Value, project.Name);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}

