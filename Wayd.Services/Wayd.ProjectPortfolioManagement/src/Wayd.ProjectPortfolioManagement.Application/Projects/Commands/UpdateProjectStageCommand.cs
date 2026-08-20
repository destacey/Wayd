using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record UpdateProjectStageCommand(
    Guid ProjectId,
    Guid StageId,
    string Description,
    int Status,
    LocalDate? PlannedStart,
    LocalDate? PlannedEnd,
    decimal Progress,
    List<Guid>? AssigneeIds) : ICommand;

public sealed class UpdateProjectStageCommandValidator : CustomValidator<UpdateProjectStageCommand>
{
    public UpdateProjectStageCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.Status).Must(s => Enum.IsDefined(typeof(TaskStatus), s))
            .WithMessage("Invalid status value.");
        RuleFor(x => x.Progress).InclusiveBetween(0, 100);
    }
}

public sealed class UpdateProjectStageCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ILogger<UpdateProjectStageCommandHandler> logger)
    : ICommandHandler<UpdateProjectStageCommand>
{
    private const string AppRequestName = nameof(UpdateProjectStageCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<UpdateProjectStageCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateProjectStageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var project = await _ppmDbContext.Projects
                .AsSplitQuery()
                .Include(p => p.Stages)
                .ThenInclude(p => p.Roles)
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found.", request.ProjectId);
                return Result.Failure($"Project {request.ProjectId} not found.");
            }

            var stage = project.Stages.FirstOrDefault(p => p.Id == request.StageId);
            if (stage is null)
            {
                _logger.LogInformation("Project Stage {StageId} not found for Project {ProjectId}.", request.StageId, request.ProjectId);
                return Result.Failure($"Project Stage {request.StageId} not found.");
            }

            var descriptionResult = stage.UpdateDescription(request.Description);
            if (descriptionResult.IsFailure)
                return await HandleDomainFailure(project, descriptionResult, cancellationToken);

            var statusResult = stage.UpdateStatus((TaskStatus)request.Status);
            if (statusResult.IsFailure)
                return await HandleDomainFailure(project, statusResult, cancellationToken);

            FlexibleDateRange? dateRange = request.PlannedStart.HasValue && request.PlannedEnd.HasValue
                ? new FlexibleDateRange(request.PlannedStart.Value, request.PlannedEnd.Value)
                : null;

            var datesResult = project.UpdateStageDates(stage.Id, dateRange);
            if (datesResult.IsFailure)
                return await HandleDomainFailure(project, datesResult, cancellationToken);

            var progressResult = stage.UpdateProgress(new Progress(request.Progress));
            if (progressResult.IsFailure)
                return await HandleDomainFailure(project, progressResult, cancellationToken);

            if (request.AssigneeIds is not null)
            {
                var updatedRoles = new Dictionary<ProjectStageRole, HashSet<Guid>>
                {
                    { ProjectStageRole.Assignee, [.. request.AssigneeIds] }
                };

                var rolesResult = stage.UpdateRoles(updatedRoles);
                if (rolesResult.IsFailure)
                    return await HandleDomainFailure(project, rolesResult, cancellationToken);
            }

            await _ppmDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project Stage {StageId} updated for Project {ProjectId}.", request.StageId, request.ProjectId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }

    private async Task<Result> HandleDomainFailure(Project project, Result errorResult, CancellationToken cancellationToken)
    {
        try
        {
            await _ppmDbContext.Entry(project).ReloadAsync(cancellationToken);
            foreach (var task in project.Tasks)
            {
                await _ppmDbContext.Entry(task).ReloadAsync(cancellationToken);
                task.ClearDomainEvents();
            }
            foreach (var stage in project.Stages)
            {
                await _ppmDbContext.Entry(stage).ReloadAsync(cancellationToken);
                stage.ClearDomainEvents();
            }
        }
        catch (NotImplementedException)
        {
            foreach (var task in project.Tasks)
            {
                task.ClearDomainEvents();
            }
            foreach (var stage in project.Stages)
            {
                stage.ClearDomainEvents();
            }
        }

        _logger.LogError("Unable to update project stage. Error message: {Error}", errorResult.Error);
        return Result.Failure(errorResult.Error);
    }
}
