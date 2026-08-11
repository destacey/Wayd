using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

/// <summary>
/// Command to change the Program of a Project.
/// </summary>
/// <param name="Id"></param>
/// <param name="ProgramId">The new ProgramId to assign to the Project.  If null, the Program will be removed.</param>
public sealed record ChangeProjectProgramCommand(Guid Id, Guid? ProgramId) : ICommand, IRequireLinkedEmployee;

public sealed class ChangeProjectProgramCommandValidator : AbstractValidator<ChangeProjectProgramCommand>
{
    public ChangeProjectProgramCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();

        RuleFor(x => x.ProgramId)
            .Must(id => id == null || id != Guid.Empty)
            .WithMessage("ProgramId cannot be an empty GUID.");
    }
}

public sealed class ChangeProjectProgramCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser, ILogger<ChangeProjectProgramCommandHandler> logger) : ICommandHandler<ChangeProjectProgramCommand>
{
    private const string AppRequestName = nameof(ChangeProjectProgramCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ChangeProjectProgramCommandHandler> _logger = logger;

    public async Task<Result> Handle(ChangeProjectProgramCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var actor = await _currentPrincipal.ResolvePpmActor(_currentUser, cancellationToken);

            // The portfolio assembles the project's ancestry itself, so its own roles and its programs'
            // roles both have to be loaded alongside the projects it owns.
            var project = await _projectPortfolioManagementDbContext.Projects
                .AsSplitQuery()
                .Include(p => p.Program)
                .Include(p => p.Portfolio!)
                    .ThenInclude(p => p.Programs)
                        .ThenInclude(p => p.Roles)
                .Include(p => p.Portfolio!)
                    .ThenInclude(p => p.Roles)
                .Include(p => p.Portfolio!)
                    .ThenInclude(p => p.Projects)
                        .ThenInclude(p => p.Roles)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found.", request.Id);
                return Result.Failure("Project not found.");
            }

            var portfolio = project.Portfolio;

            var changeResult = portfolio!.ChangeProjectProgram(actor, project.Id, request.ProgramId);
            if (changeResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(project).ReloadAsync(cancellationToken);
                project.ClearDomainEvents();

                _logger.LogError("Unable to change the program for project {ProjectId}.  Error message: {Error}", request.Id, changeResult.Error);
                return Result.Failure(changeResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project {ProjectId} changed from program {OldProgramId} to {NewProgramId}.", request.Id, project.ProgramId, request.ProgramId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
