using Wayd.Common.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.HealthChecks.Commands;

public sealed record CreateProjectHealthCheckCommand(
    Guid ProjectId,
    HealthStatus Status,
    Instant Expiration,
    string? Note) : ICommand<Guid>;

public sealed class CreateProjectHealthCheckCommandValidator
    : CustomValidator<CreateProjectHealthCheckCommand>
{
    public CreateProjectHealthCheckCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.ProjectId)
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

public sealed class CreateProjectHealthCheckCommandHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<CreateProjectHealthCheckCommandHandler> logger)
    : ICommandHandler<CreateProjectHealthCheckCommand, Guid>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<CreateProjectHealthCheckCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CreateProjectHealthCheckCommand request, CancellationToken cancellationToken)
    {
        Guid? employeeId = _currentUser.GetEmployeeId();
        if (employeeId is null)
            return Result.Failure<Guid>("Unable to determine the current user's employee Id.");

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
            return Result.Failure<Guid>($"Project {request.ProjectId} not found.");
        }

        var addResult = project.AddHealthCheck(
            request.Status,
            employeeId.Value,
            project.Portfolio!.Roles,
            project.Program?.Roles,
            request.Expiration,
            request.Note,
            _dateTimeProvider.Now);

        if (addResult.IsFailure)
        {
            _logger.LogError("Unable to add health check to project {ProjectId}. Error: {Error}", request.ProjectId, addResult.Error);
            return Result.Failure<Guid>(addResult.Error);
        }

        await _ppmDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(addResult.Value.Id);
    }
}
