namespace Wayd.ProjectPortfolioManagement.Application.Projects.HealthChecks.Commands;

public sealed record DeleteProjectHealthCheckCommand(
    Guid ProjectId,
    Guid HealthCheckId) : ICommand;

public sealed class DeleteProjectHealthCheckCommandValidator
    : CustomValidator<DeleteProjectHealthCheckCommand>
{
    public DeleteProjectHealthCheckCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.ProjectId)
            .NotEmpty();

        RuleFor(c => c.HealthCheckId)
            .NotEmpty();
    }
}

public sealed class DeleteProjectHealthCheckCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentUser currentUser,
    ILogger<DeleteProjectHealthCheckCommandHandler> logger)
    : ICommandHandler<DeleteProjectHealthCheckCommand>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<DeleteProjectHealthCheckCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteProjectHealthCheckCommand request, CancellationToken cancellationToken)
    {
        Guid? employeeId = _currentUser.GetEmployeeId();
        if (employeeId is null)
            return Result.Failure("Unable to determine the current user's employee Id.");

        var project = await _ppmDbContext.Projects
            .AsSplitQuery()
            .Include(p => p.Roles)
            .Include(p => p.HealthChecks)
            .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
            .Include(p => p.Program).ThenInclude(p => p!.Roles)
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (project is null)
        {
            _logger.LogInformation("Project {ProjectId} not found.", request.ProjectId);
            return Result.Failure($"Project {request.ProjectId} not found.");
        }

        var removeResult = project.RemoveHealthCheck(
            request.HealthCheckId,
            employeeId.Value,
            project.Portfolio!.Roles,
            project.Program?.Roles);

        if (removeResult.IsFailure)
        {
            _logger.LogError("Unable to remove health check {HealthCheckId} from project {ProjectId}. Error: {Error}", request.HealthCheckId, request.ProjectId, removeResult.Error);
            return Result.Failure(removeResult.Error);
        }

        await _ppmDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
