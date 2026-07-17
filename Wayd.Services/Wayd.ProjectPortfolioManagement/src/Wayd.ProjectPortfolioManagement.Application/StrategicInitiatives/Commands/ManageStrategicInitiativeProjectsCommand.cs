namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;

public sealed record ManageStrategicInitiativeProjectsCommand(Guid Id, List<Guid> ProjectIds) : ICommand;

public sealed class ManageStrategicInitiativeProjectsCommandValidator : AbstractValidator<ManageStrategicInitiativeProjectsCommand>
{
    public ManageStrategicInitiativeProjectsCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();

        RuleFor(v => v.ProjectIds)
            .NotNull();
    }
}

public sealed class ManageStrategicInitiativeProjectsCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<ManageStrategicInitiativeProjectsCommandHandler> logger) : ICommandHandler<ManageStrategicInitiativeProjectsCommand>
{
    private const string AppRequestName = nameof(ManageStrategicInitiativeProjectsCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ManageStrategicInitiativeProjectsCommandHandler> _logger = logger;
    public async Task<Result> Handle(ManageStrategicInitiativeProjectsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategicInitiative = await _ppmDbContext.StrategicInitiatives
                .Include(i => i.StrategicInitiativeProjects)
                .SingleOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
            if (strategicInitiative == null)
            {
                _logger.LogInformation("Strategic Initiative with Id {StrategicInitiativeId} not found.", request.Id);
                return Result.Failure("Strategic Initiative not found.");
            }

            // validate projects exist
            var projectIds = request.ProjectIds.ToList();

            var existingProjectCount = await _ppmDbContext.Projects
                .CountAsync(p => projectIds.Contains(p.Id), cancellationToken);
            if (existingProjectCount != projectIds.Count)
            {
                _logger.LogError("Unable to update projects for Strategic Initiative {StrategicInitiativeId} because one or more projects do not exist.", request.Id);
                return Result.Failure("One or more projects do not exist.");
            }

            var result = strategicInitiative.ManageProjects(projectIds);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to update projects for Strategic Initiative {StrategicInitiativeId}. Error message: {Error}", request.Id, result.Error);
                return Result.Failure(result.Error);
            }

            await _ppmDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Successfully updated projects for Strategic Initiative {StrategicInitiativeId}.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
