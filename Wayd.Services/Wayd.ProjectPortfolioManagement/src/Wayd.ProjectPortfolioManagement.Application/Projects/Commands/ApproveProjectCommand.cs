using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record ApproveProjectCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

public sealed class ApproveProjectCommandValidator : AbstractValidator<ApproveProjectCommand>
{
    public ApproveProjectCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ApproveProjectCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ICurrentPrincipal currentPrincipal,
    ILogger<ApproveProjectCommandHandler> logger) : ICommandHandler<ApproveProjectCommand>
{
    private const string AppRequestName = nameof(ApproveProjectCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<ApproveProjectCommandHandler> _logger = logger;

    public async Task<Result> Handle(ApproveProjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actor = await _currentPrincipal.ResolvePpmActor(cancellationToken);

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

            var approveResult = project.Approve(actor, project.AncestryRoles());
            if (approveResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(project).ReloadAsync(cancellationToken);
                project.ClearDomainEvents();

                _logger.LogError("Unable to approve Project {ProjectId}.  Error message: {Error}", request.Id, approveResult.Error);
                return Result.Failure(approveResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project {ProjectId} approved.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
