using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.Validators;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

/// <summary>
/// Command to change the Key of a Project.
/// </summary>
/// <param name="Id"></param>
/// <param name="Key">The new Key to assign to the Project.</param>
public sealed record ChangeProjectKeyCommand(Guid Id, ProjectKey Key) : ICommand;

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

internal sealed class ChangeProjectKeyCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ChangeProjectKeyCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeProjectKeyCommand>
{
    private const string AppRequestName = nameof(ChangeProjectKeyCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ChangeProjectKeyCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ChangeProjectKeyCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var project = await _projectPortfolioManagementDbContext.Projects
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found.", request.Id);
                return Result.Failure("Project not found.");
            }

            var originalKey = project.Key;
            var newKey = request.Key;

            var changeResult = project.ChangeKey(newKey, _dateTimeProvider.Now);
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
