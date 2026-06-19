using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

public sealed record CreateProjectLifecycleCommand(
    string Name,
    string Description,
    List<CreateProjectLifecycleCommand.PhaseInput>? Phases)
    : ICommand<Guid>
{
    public sealed record PhaseInput(string Name, string Description);
}

public sealed class CreateProjectLifecycleCommandValidator : AbstractValidator<CreateProjectLifecycleCommand>
{
    public CreateProjectLifecycleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);

        RuleForEach(x => x.Phases).ChildRules(phase =>
        {
            phase.RuleFor(p => p.Name)
                .NotEmpty()
                .MaximumLength(32);

            phase.RuleFor(p => p.Description)
                .NotEmpty()
                .MaximumLength(1024);
        });
    }
}

internal sealed class CreateProjectLifecycleCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<CreateProjectLifecycleCommandHandler> logger)
    : ICommandHandler<CreateProjectLifecycleCommand, Guid>
{
    private const string AppRequestName = nameof(CreateProjectLifecycleCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<CreateProjectLifecycleCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CreateProjectLifecycleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var phases = request.Phases?
                .Select(p => (p.Name, p.Description))
                .ToList();

            var lifecycle = ProjectLifecycle.Create(
                request.Name,
                request.Description,
                phases
                );

            await _projectPortfolioManagementDbContext.ProjectLifecycles.AddAsync(lifecycle, cancellationToken);
            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project Lifecycle {ProjectLifecycleId} created.", lifecycle.Id);

            return Result.Success(lifecycle.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
