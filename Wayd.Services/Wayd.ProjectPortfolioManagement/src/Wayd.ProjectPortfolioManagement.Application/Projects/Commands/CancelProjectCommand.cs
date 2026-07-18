namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record CancelProjectCommand(Guid Id) : ICommand;

public sealed class CancelProjectCommandValidator : AbstractValidator<CancelProjectCommand>
{
    public CancelProjectCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class CancelProjectCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<CancelProjectCommandHandler> logger) : ICommandHandler<CancelProjectCommand>
{
    private const string AppRequestName = nameof(CancelProjectCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<CancelProjectCommandHandler> _logger = logger;

    public async Task<Result> Handle(CancelProjectCommand request, CancellationToken cancellationToken)
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

            var cancelResult = project.Cancel();
            if (cancelResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(project).ReloadAsync(cancellationToken);
                project.ClearDomainEvents();

                _logger.LogError("Unable to cancel Project {ProjectId}.  Error message: {Error}", request.Id, cancelResult.Error);
                return Result.Failure(cancelResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project {ProjectId} cancelled.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
