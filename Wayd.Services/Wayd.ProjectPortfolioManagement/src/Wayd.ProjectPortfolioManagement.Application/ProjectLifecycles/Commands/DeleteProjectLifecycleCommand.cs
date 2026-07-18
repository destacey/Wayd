namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

public sealed record DeleteProjectLifecycleCommand(Guid Id) : ICommand;

public sealed class DeleteProjectLifecycleCommandValidator : AbstractValidator<DeleteProjectLifecycleCommand>
{
    public DeleteProjectLifecycleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class DeleteProjectLifecycleCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<DeleteProjectLifecycleCommandHandler> logger)
    : ICommandHandler<DeleteProjectLifecycleCommand>
{
    private const string AppRequestName = nameof(DeleteProjectLifecycleCommand);
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<DeleteProjectLifecycleCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteProjectLifecycleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var lifecycle = await _projectPortfolioManagementDbContext.ProjectLifecycles
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
            if (lifecycle is null)
            {
                _logger.LogInformation("Project Lifecycle {ProjectLifecycleId} not found.", request.Id);
                return Result.Failure("Project Lifecycle not found.");
            }

            if (!lifecycle.CanBeDeleted())
            {
                _logger.LogInformation("Project Lifecycle {ProjectLifecycleId} cannot be deleted.", request.Id);
                return Result.Failure("Project Lifecycle cannot be deleted.");
            }

            _projectPortfolioManagementDbContext.ProjectLifecycles.Remove(lifecycle);
            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project Lifecycle {ProjectLifecycleId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
