using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record CancelProjectCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

public sealed class CancelProjectCommandValidator : AbstractValidator<CancelProjectCommand>
{
    public CancelProjectCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class CancelProjectCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IStatusResolver statusResolver,
    ILogger<CancelProjectCommandHandler> logger) : ICommandHandler<CancelProjectCommand>
{
    private const string AppRequestName = nameof(CancelProjectCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ILogger<CancelProjectCommandHandler> _logger = logger;

    public async Task<Result> Handle(CancelProjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

            var project = await _projectPortfolioManagementDbContext.Projects
                .AsSplitQuery()
                .Include(p => p.Roles)
                .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
                .Include(p => p.Program).ThenInclude(p => p!.Roles)
                // Cancel descopes every open deliverable; unloaded, the set reads as empty and leaves them open
                // on a canceled project.
                .Include(p => p.Deliverables)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found.", request.Id);
                return Result.Failure("Project not found.");
            }

            // Resolved only when something is open, so a project without deliverables cancels even while the
            // deliverable workflow is misconfigured.
            StatusRef? descoped = null;
            if (project.Deliverables.Any(d => d.IsOpen))
            {
                var resolved = await _statusResolver.ForAlias(PpmWorkflowOwners.ProjectDeliverable.Key, scopeId: null,
                    (int)ProjectDeliverableStatusAlias.Descoped, cancellationToken);
                if (resolved.IsFailure)
                    return Result.Failure(resolved.Error);

                descoped = resolved.Value;
            }

            var cancelResult = project.Cancel(actor, project.AncestryRoles(), _dateTimeProvider.Now, descopedStatus: descoped);
            if (cancelResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(project).ReloadAsync(cancellationToken);
                project.ClearDomainEvents();

                _logger.LogError("Unable to cancel Project {ProjectId}.  Error message: {Error}", request.Id, cancelResult.Error);
                return Result.Failure(cancelResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project {ProjectId} canceled.", request.Id);

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ProjectSaves.StaleError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command.", AppRequestName);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
