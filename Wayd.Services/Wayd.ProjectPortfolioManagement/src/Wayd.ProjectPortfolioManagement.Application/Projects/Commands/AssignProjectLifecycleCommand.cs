using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record AssignProjectLifecycleCommand(Guid ProjectId, Guid LifecycleId) : ICommand, IRequireLinkedEmployee;

public sealed class AssignProjectLifecycleCommandValidator : CustomValidator<AssignProjectLifecycleCommand>
{
    public AssignProjectLifecycleCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty();

        RuleFor(x => x.LifecycleId)
            .NotEmpty();
    }
}

public sealed class AssignProjectLifecycleCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    ILogger<AssignProjectLifecycleCommandHandler> logger)
    : ICommandHandler<AssignProjectLifecycleCommand>
{
    private const string AppRequestName = nameof(AssignProjectLifecycleCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<AssignProjectLifecycleCommandHandler> _logger = logger;

    public async Task<Result> Handle(AssignProjectLifecycleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

            var project = await _ppmDbContext.Projects
                .AsSplitQuery()
                .Include(p => p.Stages)
                .Include(p => p.Roles)
                .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
                .Include(p => p.Program).ThenInclude(p => p!.Roles)
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found.", request.ProjectId);
                return Result.Failure($"Project {request.ProjectId} not found.");
            }

            var lifecycle = await _ppmDbContext.ProjectLifecycles
                .Include(l => l.Stages)
                .FirstOrDefaultAsync(l => l.Id == request.LifecycleId, cancellationToken);

            if (lifecycle is null)
            {
                _logger.LogInformation("Project Lifecycle {LifecycleId} not found.", request.LifecycleId);
                return Result.Failure($"Project Lifecycle {request.LifecycleId} not found.");
            }

            var result = project.AssignLifecycle(actor, project.AncestryRoles(), lifecycle);
            if (result.IsFailure)
            {
                _logger.LogWarning("Unable to assign lifecycle to project {ProjectId}. Error: {Error}", request.ProjectId, result.Error);
                return result;
            }

            await _ppmDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Lifecycle {LifecycleId} assigned to Project {ProjectId}.", request.LifecycleId, request.ProjectId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
