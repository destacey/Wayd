using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record CompleteProjectCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

public sealed class CompleteProjectCommandValidator : AbstractValidator<CompleteProjectCommand>
{
    public CompleteProjectCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class CompleteProjectCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<CompleteProjectCommandHandler> logger) : ICommandHandler<CompleteProjectCommand>
{
    private const string AppRequestName = nameof(CompleteProjectCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CompleteProjectCommandHandler> _logger = logger;

    public async Task<Result> Handle(CompleteProjectCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

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

            var completeResult = project.Complete(actor, project.AncestryRoles(), _dateTimeProvider.Now);
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
