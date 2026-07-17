namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

public sealed record ArchiveProjectLifecycleCommand(Guid Id) : ICommand;

public sealed class ArchiveProjectLifecycleCommandValidator : AbstractValidator<ArchiveProjectLifecycleCommand>
{
    public ArchiveProjectLifecycleCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ArchiveProjectLifecycleCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ArchiveProjectLifecycleCommandHandler> logger)
    : ICommandHandler<ArchiveProjectLifecycleCommand>
{
    private const string AppRequestName = nameof(ArchiveProjectLifecycleCommand);
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ArchiveProjectLifecycleCommandHandler> _logger = logger;

    public async Task<Result> Handle(ArchiveProjectLifecycleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var lifecycle = await _projectPortfolioManagementDbContext.ProjectLifecycles
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (lifecycle is null)
            {
                _logger.LogInformation("Project Lifecycle {ProjectLifecycleId} not found.", request.Id);
                return Result.Failure("Project Lifecycle not found.");
            }

            var archiveResult = lifecycle.Archive();
            if (archiveResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(lifecycle).ReloadAsync(cancellationToken);
                lifecycle.ClearDomainEvents();

                _logger.LogError("Unable to archive Project Lifecycle {ProjectLifecycleId}.  Error message: {Error}", request.Id, archiveResult.Error);

                return Result.Failure(archiveResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project Lifecycle {ProjectLifecycleId} archived.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
