using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Commands;

public sealed record UpdateProjectPhaseCommand(
    Guid ProjectId,
    Guid PhaseId,
    string Description,
    int Status,
    LocalDate? PlannedStart,
    LocalDate? PlannedEnd,
    decimal Progress,
    List<Guid>? AssigneeIds) : ICommand;

public sealed class UpdateProjectPhaseCommandValidator : CustomValidator<UpdateProjectPhaseCommand>
{
    public UpdateProjectPhaseCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.PhaseId).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.Status).Must(s => Enum.IsDefined(typeof(TaskStatus), s))
            .WithMessage("Invalid status value.");
        RuleFor(x => x.Progress).InclusiveBetween(0, 100);
    }
}

public sealed class UpdateProjectPhaseCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ILogger<UpdateProjectPhaseCommandHandler> logger)
    : ICommandHandler<UpdateProjectPhaseCommand>
{
    private const string AppRequestName = nameof(UpdateProjectPhaseCommand);

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<UpdateProjectPhaseCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateProjectPhaseCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var project = await _ppmDbContext.Projects
                .AsSplitQuery()
                .Include(p => p.Phases)
                .ThenInclude(p => p.Roles)
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

            if (project is null)
            {
                _logger.LogInformation("Project {ProjectId} not found.", request.ProjectId);
                return Result.Failure($"Project {request.ProjectId} not found.");
            }

            var phase = project.Phases.FirstOrDefault(p => p.Id == request.PhaseId);
            if (phase is null)
            {
                _logger.LogInformation("Project Phase {PhaseId} not found for Project {ProjectId}.", request.PhaseId, request.ProjectId);
                return Result.Failure($"Project Phase {request.PhaseId} not found.");
            }

            var descriptionResult = phase.UpdateDescription(request.Description);
            if (descriptionResult.IsFailure)
                return await HandleDomainFailure(project, descriptionResult, cancellationToken);

            var statusResult = phase.UpdateStatus((TaskStatus)request.Status);
            if (statusResult.IsFailure)
                return await HandleDomainFailure(project, statusResult, cancellationToken);

            FlexibleDateRange? dateRange = request.PlannedStart.HasValue && request.PlannedEnd.HasValue
                ? new FlexibleDateRange(request.PlannedStart.Value, request.PlannedEnd.Value)
                : null;

            var datesResult = project.UpdatePhaseDates(phase.Id, dateRange);
            if (datesResult.IsFailure)
                return await HandleDomainFailure(project, datesResult, cancellationToken);

            var progressResult = phase.UpdateProgress(new Progress(request.Progress));
            if (progressResult.IsFailure)
                return await HandleDomainFailure(project, progressResult, cancellationToken);

            if (request.AssigneeIds is not null)
            {
                var updatedRoles = new Dictionary<ProjectPhaseRole, HashSet<Guid>>
                {
                    { ProjectPhaseRole.Assignee, [.. request.AssigneeIds] }
                };

                var rolesResult = phase.UpdateRoles(updatedRoles);
                if (rolesResult.IsFailure)
                    return await HandleDomainFailure(project, rolesResult, cancellationToken);
            }

            await _ppmDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project Phase {PhaseId} updated for Project {ProjectId}.", request.PhaseId, request.ProjectId);

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
            foreach (var phase in project.Phases)
            {
                await _ppmDbContext.Entry(phase).ReloadAsync(cancellationToken);
                phase.ClearDomainEvents();
            }
        }
        catch (NotImplementedException)
        {
            foreach (var task in project.Tasks)
            {
                task.ClearDomainEvents();
            }
            foreach (var phase in project.Phases)
            {
                phase.ClearDomainEvents();
            }
        }

        _logger.LogError("Unable to update project phase. Error message: {Error}", errorResult.Error);
        return Result.Failure(errorResult.Error);
    }
}
