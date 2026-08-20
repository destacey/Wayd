using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;

/// <summary>
/// Sets the status of project stages from a batch of rows, each naming a stage within a project. The status
/// is applied verbatim through the domain's <c>UpdateStatus</c>; the import deliberately does NOT derive a
/// stage's status from its tasks, so a client using this endpoint keeps full control over stage status and
/// only the data supplied is written. (For seeding, the data generator computes each stage's status from its
/// tasks and emits these rows.)
/// <para>
/// The batch is all-or-nothing: any project key or stage name that cannot be resolved fails the whole import
/// with the list of unresolved references, so it stays re-runnable.
/// </para>
/// </summary>
public sealed record ImportProjectStagesCommand : ICommand
{
    public ImportProjectStagesCommand(IEnumerable<ImportProjectStageDto> stages)
    {
        Stages = [.. stages];
    }

    public List<ImportProjectStageDto> Stages { get; }
}

public sealed class ImportProjectStagesCommandValidator : CustomValidator<ImportProjectStagesCommand>
{
    public ImportProjectStagesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Stages)
            .NotNull()
            .NotEmpty();

        RuleForEach(p => p.Stages)
            .NotNull()
            .SetValidator(new ImportProjectStageDtoValidator());
    }
}

public sealed class ImportProjectStagesCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ImportProjectStagesCommandHandler> logger) : ICommandHandler<ImportProjectStagesCommand>
{
    private const string RequestName = nameof(ImportProjectStagesCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ImportProjectStagesCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportProjectStagesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Key is persisted through a value converter, so compare against ProjectKey instances.
            var keys = request.Stages.Select(p => p.ProjectKey).Distinct().ToList();

            var projects = await _projectPortfolioManagementDbContext.Projects
                .Include(p => p.Stages)
                .Where(p => keys.Contains(p.Key))
                .ToListAsync(cancellationToken);

            var projectsByKey = projects.ToDictionary(p => p.Key.Value, p => p, StringComparer.OrdinalIgnoreCase);

            var unresolvedProjects = keys
                .Select(k => k.Value)
                .Where(k => !projectsByKey.ContainsKey(k))
                .ToList();
            if (unresolvedProjects.Count > 0)
                return Fail($"Could not resolve the following projects: {Quote(unresolvedProjects)}.");

            foreach (var row in request.Stages)
            {
                var project = projectsByKey[row.ProjectKey.Value];

                var matches = project.Stages
                    .Where(ph => string.Equals(ph.Name, Normalize(row.StageName), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 0)
                    return Fail($"Could not resolve stage '{row.StageName}' in project '{row.ProjectKey.Value}'. The project's lifecycle determines its stages.");
                if (matches.Count > 1)
                    return Fail($"Stage name '{row.StageName}' matches more than one stage in project '{row.ProjectKey.Value}'.");

                var result = matches[0].UpdateStatus(row.Status);
                if (result.IsFailure)
                    return Fail($"Could not set stage '{row.StageName}' in project '{row.ProjectKey.Value}' to {row.Status}: {result.Error}");
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{RequestName}: updated {Count} project stage(s).", RequestName, request.Stages.Count);

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
