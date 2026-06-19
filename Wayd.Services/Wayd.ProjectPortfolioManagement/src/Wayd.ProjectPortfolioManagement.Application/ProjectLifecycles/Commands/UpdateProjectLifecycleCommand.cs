namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

public sealed record UpdateProjectLifecycleCommand(
    Guid Id,
    string Name,
    string Description)
    : ICommand;

public sealed class UpdateProjectLifecycleCommandValidator : AbstractValidator<UpdateProjectLifecycleCommand>
{
    public UpdateProjectLifecycleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);
    }
}

internal sealed class UpdateProjectLifecycleCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<UpdateProjectLifecycleCommandHandler> logger)
    : ICommandHandler<UpdateProjectLifecycleCommand>
{
    private const string AppRequestName = nameof(UpdateProjectLifecycleCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<UpdateProjectLifecycleCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateProjectLifecycleCommand request, CancellationToken cancellationToken)
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

            var updateResult = lifecycle.Update(
                request.Name,
                request.Description
                );
            if (updateResult.IsFailure)
            {
                _logger.LogError("Unable to update Project Lifecycle {ProjectLifecycleId}.  Error message: {Error}", lifecycle.Id, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project Lifecycle {ProjectLifecycleId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
