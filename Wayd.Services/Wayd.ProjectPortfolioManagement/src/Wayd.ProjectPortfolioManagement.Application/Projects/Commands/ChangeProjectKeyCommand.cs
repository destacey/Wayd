using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.Validators;

using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

/// <summary>
/// Command to change the Key of a Project.
/// </summary>
/// <param name="Id"></param>
/// <param name="Key">The new Key to assign to the Project.</param>
public sealed record ChangeProjectKeyCommand(Guid Id, ProjectKey Key) : ICommand, IRequireLinkedEmployee;

public sealed class ChangeProjectKeyCommandValidator : AbstractValidator<ChangeProjectKeyCommand>
{
    public ChangeProjectKeyCommandValidator(IProjectPortfolioManagementDbContext ppmDbContext)
    {
        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.Key)
            .NotEmpty()
            .SetValidator(c => new ProjectKeyValidator(ppmDbContext, c.Id));
    }
}

public sealed class ChangeProjectKeyCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    ILogger<ChangeProjectKeyCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeProjectKeyCommand>
{
    private const string AppRequestName = nameof(ChangeProjectKeyCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ChangeProjectKeyCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ChangeProjectKeyCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

            var project = await _projectPortfolioManagementDbContext.Projects
                .AsSplitQuery()
                .Include(p => p.Tasks)
                .Include(p => p.Roles)
                .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
                .Include(p => p.Program).ThenInclude(p => p!.Roles)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found.", request.Id);
                return Result.Failure("Project not found.");
            }

            var originalKey = project.Key;
            var newKey = request.Key;

            var changeResult = project.ChangeKey(actor, project.AncestryRoles(), newKey, _dateTimeProvider.Now);
            if (changeResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(project).ReloadAsync(cancellationToken);
                project.ClearDomainEvents();

                _logger.LogError("Unable to change the key for project {ProjectId}.  Error message: {Error}", request.Id, changeResult.Error);
                return Result.Failure(changeResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project {ProjectId} key changed from {OriginalProjectKey} to {ProjectKey}.", request.Id, originalKey.Value, project.Key.Value);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
