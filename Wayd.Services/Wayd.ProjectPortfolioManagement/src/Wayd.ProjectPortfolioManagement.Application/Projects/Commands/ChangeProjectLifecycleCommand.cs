using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record ChangeProjectLifecycleCommand(
    Guid ProjectId,
    Guid NewLifecycleId,
    Dictionary<Guid, Guid> PhaseMapping) : ICommand;

public sealed class ChangeProjectLifecycleCommandValidator : CustomValidator<ChangeProjectLifecycleCommand>
{
    public ChangeProjectLifecycleCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.NewLifecycleId)
            .NotEmpty();

        RuleFor(x => x.PhaseMapping)
            .NotNull();
    }
}

public sealed class ChangeProjectLifecycleCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal,
    ILogger<ChangeProjectLifecycleCommandHandler> logger)
    : ICommandHandler<ChangeProjectLifecycleCommand>
{
    private const string AppRequestName = nameof(ChangeProjectLifecycleCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<ChangeProjectLifecycleCommandHandler> _logger = logger;

    public async Task<Result> Handle(ChangeProjectLifecycleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actor = await _currentPrincipal.ResolvePpmActor(cancellationToken);

            var project = await _ppmDbContext.Projects
                .AsSplitQuery()
                .Include(p => p.Phases)
                .Include(p => p.Tasks)
                .Include(p => p.Roles)
                .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
                .Include(p => p.Program).ThenInclude(p => p!.Roles)
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found.", request.ProjectId);
                return Result.Failure($"Project {request.ProjectId} not found.");
            }

            var newLifecycle = await _ppmDbContext.ProjectLifecycles
                .Include(l => l.Phases)
                .FirstOrDefaultAsync(l => l.Id == request.NewLifecycleId, cancellationToken);

            if (newLifecycle is null)
            {
                _logger.LogInformation("Project Lifecycle {LifecycleId} not found.", request.NewLifecycleId);
                return Result.Failure($"Project Lifecycle {request.NewLifecycleId} not found.");
            }

            var result = project.ChangeLifecycle(actor, project.AncestryRoles(), newLifecycle, request.PhaseMapping);
            if (result.IsFailure)
            {
                _logger.LogWarning("Unable to change lifecycle for project {ProjectId}. Error: {Error}", request.ProjectId, result.Error);
                return result;
            }

            await _ppmDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Lifecycle changed to {LifecycleId} for Project {ProjectId}.", request.NewLifecycleId, request.ProjectId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
