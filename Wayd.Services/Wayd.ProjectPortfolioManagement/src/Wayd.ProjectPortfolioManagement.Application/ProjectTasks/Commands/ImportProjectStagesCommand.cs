using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;

/// <summary>
/// Sets the status of project phases from a batch of rows, each naming a phase within a project. The status
/// is applied verbatim through the domain's <c>UpdateStatus</c>; the import deliberately does NOT derive a
/// phase's status from its tasks, so a client using this endpoint keeps full control over phase status and
/// only the data supplied is written. (For seeding, the data generator computes each phase's status from its
/// tasks and emits these rows.)
/// <para>
/// The batch is all-or-nothing: any project key or phase name that cannot be resolved fails the whole import
/// with the list of unresolved references, so it stays re-runnable.
/// </para>
/// </summary>
public sealed record ImportProjectPhasesCommand : ICommand
{
    public ImportProjectPhasesCommand(IEnumerable<ImportProjectPhaseDto> phases)
    {
        Phases = [.. phases];
    }

    public List<ImportProjectPhaseDto> Phases { get; }
}

public sealed class ImportProjectPhasesCommandValidator : CustomValidator<ImportProjectPhasesCommand>
{
    public ImportProjectPhasesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Phases)
            .NotNull()
            .NotEmpty();

        RuleForEach(p => p.Phases)
            .NotNull()
            .SetValidator(new ImportProjectPhaseDtoValidator());
    }
}

public sealed class ImportProjectPhasesCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ImportProjectPhasesCommandHandler> logger) : ICommandHandler<ImportProjectPhasesCommand>
{
    private const string RequestName = nameof(ImportProjectPhasesCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ImportProjectPhasesCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportProjectPhasesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Key is persisted through a value converter, so compare against ProjectKey instances.
            var keys = request.Phases.Select(p => p.ProjectKey).Distinct().ToList();

            var projects = await _projectPortfolioManagementDbContext.Projects
                .Include(p => p.Phases)
                .Where(p => keys.Contains(p.Key))
                .ToListAsync(cancellationToken);

            var projectsByKey = projects.ToDictionary(p => p.Key.Value, p => p, StringComparer.OrdinalIgnoreCase);

            var unresolvedProjects = keys
                .Select(k => k.Value)
                .Where(k => !projectsByKey.ContainsKey(k))
                .ToList();
            if (unresolvedProjects.Count > 0)
                return Fail($"Could not resolve the following projects: {Quote(unresolvedProjects)}.");

            foreach (var row in request.Phases)
            {
                var project = projectsByKey[row.ProjectKey.Value];

                var matches = project.Phases
                    .Where(ph => string.Equals(ph.Name, Normalize(row.PhaseName), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 0)
                    return Fail($"Could not resolve phase '{row.PhaseName}' in project '{row.ProjectKey.Value}'. The project's lifecycle determines its phases.");
                if (matches.Count > 1)
                    return Fail($"Phase name '{row.PhaseName}' matches more than one phase in project '{row.ProjectKey.Value}'.");

                var result = matches[0].UpdateStatus(row.Status);
                if (result.IsFailure)
                    return Fail($"Could not set phase '{row.PhaseName}' in project '{row.ProjectKey.Value}' to {row.Status}: {result.Error}");
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{RequestName}: updated {Count} project phase(s).", RequestName, request.Phases.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {RequestName}", RequestName);

            return Result.Failure($"Exception for request {RequestName}: {ex.Message}");
        }
    }

    private Result Fail(string message)
    {
        _logger.LogWarning("{RequestName}: {Message}", RequestName, message);
        return Result.Failure(message);
    }

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
