using Wayd.Common.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.HealthChecks.Commands;

public sealed record UpdateProjectHealthCheckCommand(
    Guid ProjectId,
    Guid HealthCheckId,
    HealthStatus Status,
    Instant Expiration,
    string? Note) : ICommand<ProjectHealthCheckDetailsDto>;

public sealed class UpdateProjectHealthCheckCommandValidator
    : CustomValidator<UpdateProjectHealthCheckCommand>
{
    public UpdateProjectHealthCheckCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.ProjectId)
            .NotEmpty();

        RuleFor(c => c.HealthCheckId)
            .NotEmpty();

        RuleFor(c => c.Status)
            .IsInEnum()
            .WithMessage("A valid health status must be selected.");

        RuleFor(c => c.Expiration)
            .NotEmpty()
            .GreaterThan(dateTimeProvider.Now)
            .WithMessage("The Expiration must be in the future.");

        RuleFor(c => c.Note)
            .MaximumLength(1024);
    }
}

public sealed class UpdateProjectHealthCheckCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<UpdateProjectHealthCheckCommandHandler> logger)
    : ICommandHandler<UpdateProjectHealthCheckCommand, ProjectHealthCheckDetailsDto>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateProjectHealthCheckCommandHandler> _logger = logger;

    public async Task<Result<ProjectHealthCheckDetailsDto>> Handle(UpdateProjectHealthCheckCommand request, CancellationToken cancellationToken)
    {
        Guid? employeeId = _currentUser.GetEmployeeId();
        if (employeeId is null)
            return Result.Failure<ProjectHealthCheckDetailsDto>("Unable to determine the current user's employee Id.");

        var project = await _ppmDbContext.Projects
            .AsSplitQuery()
            .Include(p => p.Roles)
            .Include(p => p.HealthChecks).ThenInclude(h => h.ReportedBy)
            .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
            .Include(p => p.Program).ThenInclude(p => p!.Roles)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (project is null)
        {
            _logger.LogInformation("Project {ProjectId} not found.", request.ProjectId);
            return Result.Failure<ProjectHealthCheckDetailsDto>($"Project {request.ProjectId} not found.");
        }

        var updateResult = project.UpdateHealthCheck(
            request.HealthCheckId,
            employeeId.Value,
            project.Portfolio!.Roles,
            project.Program?.Roles,
            request.Status,
            request.Expiration,
            request.Note,
            _dateTimeProvider.Now);

        if (updateResult.IsFailure)
        {
            await _ppmDbContext.Entry(project).ReloadAsync(cancellationToken);
            project.ClearDomainEvents();

            _logger.LogError("Unable to update health check {HealthCheckId} on project {ProjectId}. Error: {Error}", request.HealthCheckId, request.ProjectId, updateResult.Error);
            return Result.Failure<ProjectHealthCheckDetailsDto>(updateResult.Error);
        }

        await _ppmDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(updateResult.Value.Adapt<ProjectHealthCheckDetailsDto>());
    }
}
