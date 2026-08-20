using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

public sealed record CreateProjectLifecycleCommand(
    string Name,
    string Description,
    List<CreateProjectLifecycleCommand.StageInput>? Stages)
    : ICommand<Guid>
{
    public sealed record StageInput(string Name, string Description);
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

        RuleForEach(x => x.Stages).ChildRules(stage =>
        {
            stage.RuleFor(p => p.Name)
                .NotEmpty()
                .MaximumLength(32);

            stage.RuleFor(p => p.Description)
                .NotEmpty()
                .MaximumLength(1024);
        });
    }
}

public sealed class CreateProjectLifecycleCommandHandler(
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
            var stages = request.Stages?
                .Select(p => (p.Name, p.Description))
                .ToList();

            var lifecycle = ProjectLifecycle.Create(
                request.Name,
                request.Description,
                stages
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
