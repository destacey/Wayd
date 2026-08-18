using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record RevertProjectStatusCommand(Guid Id, ProjectStatus ToStatus, string Reason) : ICommand, IRequireLinkedEmployee;

public sealed class RevertProjectStatusCommandValidator : AbstractValidator<RevertProjectStatusCommand>
{
    public RevertProjectStatusCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();

        RuleFor(v => v.ToStatus)
            .IsInEnum();

        // The aggregate requires a reason too, so a caller that bypasses this validator still cannot
        // record a reversal without one. Length is bounded to the column.
        RuleFor(v => v.Reason)
            .NotEmpty()
            .MaximumLength(1024);
    }
}

public sealed class RevertProjectStatusCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<RevertProjectStatusCommandHandler> logger) : ICommandHandler<RevertProjectStatusCommand>
{
    private const string AppRequestName = nameof(RevertProjectStatusCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<RevertProjectStatusCommandHandler> _logger = logger;

    public async Task<Result> Handle(RevertProjectStatusCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

            // Portfolio and Program are needed twice over: their roles carry the actor's delivery
            // leadership, and their own status decides whether a child may reopen underneath them.
            var project = await _projectPortfolioManagementDbContext.Projects
                .AsSplitQuery()
                .Include(p => p.Roles)
                .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
                .Include(p => p.Program).ThenInclude(p => p!.Roles)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found.", request.Id);
                return Result.Failure("Project not found.");
            }

            var revertResult = project.RevertStatus(actor, project.AncestryRoles(), request.ToStatus, request.Reason, _dateTimeProvider.Now);
            if (revertResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(project).ReloadAsync(cancellationToken);
                project.ClearDomainEvents();

                _logger.LogError("Unable to revert Project {ProjectId} to {ToStatus}.  Error message: {Error}", request.Id, request.ToStatus, revertResult.Error);
                return Result.Failure(revertResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project {ProjectId} status reverted to {ToStatus}.", request.Id, request.ToStatus);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
