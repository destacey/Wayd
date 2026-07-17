namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record CompleteProjectCommand(Guid Id) : ICommand;

public sealed class CompleteProjectCommandValidator : AbstractValidator<CompleteProjectCommand>
{
    public CompleteProjectCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class CompleteProjectCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<CompleteProjectCommandHandler> logger) : ICommandHandler<CompleteProjectCommand>
{
    private const string AppRequestName = nameof(CompleteProjectCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<CompleteProjectCommandHandler> _logger = logger;

    public async Task<Result> Handle(CompleteProjectCommand request, CancellationToken cancellationToken)
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

            var completeResult = project.Complete();
            if (completeResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(project).ReloadAsync(cancellationToken);
                project.ClearDomainEvents();

                _logger.LogError("Unable to complete Project {ProjectId}.  Error message: {Error}", request.Id, completeResult.Error);
                return Result.Failure(completeResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project {ProjectId} completed.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
