namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record ActivateProjectCommand(Guid Id) : ICommand;

public sealed class ActivateProjectCommandValidator : AbstractValidator<ActivateProjectCommand>
{
    public ActivateProjectCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ActivateProjectCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<ActivateProjectCommandHandler> logger) : ICommandHandler<ActivateProjectCommand>
{
    private const string AppRequestName = nameof(ActivateProjectCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ActivateProjectCommandHandler> _logger = logger;

    public async Task<Result> Handle(ActivateProjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var project = await _projectPortfolioManagementDbContext.Projects
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found.", request.Id);
                return Result.Failure("Project not found.");
            }

            var activateResult = project.Activate();
            if (activateResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(project).ReloadAsync(cancellationToken);
                project.ClearDomainEvents();

                _logger.LogError("Unable to activate Project {ProjectId}.  Error message: {Error}", request.Id, activateResult.Error);
                return Result.Failure(activateResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project {ProjectId} activated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
