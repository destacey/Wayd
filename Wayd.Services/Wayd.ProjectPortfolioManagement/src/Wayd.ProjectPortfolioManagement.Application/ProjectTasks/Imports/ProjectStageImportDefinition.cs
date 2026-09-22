using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Imports;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Imports;

/// <summary>
/// Sets the status of project stages, each row naming one stage within one project.
/// </summary>
/// <remarks>
/// The status is applied verbatim through the domain's <c>UpdateStatus</c>: the import deliberately does
/// not derive a stage's status from its tasks, so whatever produced the file keeps full control and only
/// what it supplied is written.
/// <para>
/// Per group, keyed on the project, because a project's stages are read against one another: one reported
/// complete while a later one is still not started is a statement about the project, not about a stage. A
/// rejected row therefore keeps that project's other stages at whatever they already said, and leaves every
/// other project alone.
/// </para>
/// </remarks>
public sealed class ProjectStageImportDefinition(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    IImportPayloadSerializer serializer) : ImportDefinition<ImportProjectStageDto>(serializer)
{
    public const string ImportKey = "ppm.project-stages";

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public override string Key => ImportKey;
    public override string DisplayName => "Project Stages";
    public override string PermissionAction => ApplicationAction.Import;
    public override string PermissionResource => ApplicationResource.Projects;

    public override ImportAtomicity Atomicity => ImportAtomicity.PerGroup;
    public override string? GroupNoun => "project";

    // Saving chunk by chunk makes a larger file possible, but a run at that size is not yet proven end to
    // end, so the cap stays where the all-or-nothing version had it. The preflight bound follows it, and
    // would have to anyway: a preflight is one transaction rolled back at the end, so it cannot release
    // its locks chunk by chunk the way a real run does — there, the file size is the lock time.
    public override int MaxRows => 10_000;

    // Canonical by construction: ProjectKey trims and uppercases, so two rows spelling a key differently
    // still land in the same group.
    protected override string? GroupKey(ImportProjectStageDto row) => row.ProjectKey.Value;

    protected override IReadOnlyList<ImportPass<ImportProjectStageDto>> Steps =>
    [
        new("UpdateStages", ImportPassScope.Chunked, UpdateStages),
    ];

    private async Task<Result> UpdateStages(ImportPassContext<ImportProjectStageDto> context, CancellationToken cancellationToken)
    {
        var projectsByKey = await ResolveProjects(context, cancellationToken);

        foreach (var row in context.Accepted)
        {
            var data = row.Data;
            var key = data.ProjectKey.Value;

            if (!projectsByKey.TryGetValue(key, out var project))
            {
                row.Failed($"No project was found with key '{key}'.");
                continue;
            }

            var name = Normalize(data.StageName);
            var matches = project.Stages
                .Where(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                row.Failed($"No stage named '{data.StageName}' was found in project '{key}'. The project's lifecycle determines its stages.");
                continue;
            }

            if (matches.Count > 1)
            {
                row.Failed($"Stage name '{data.StageName}' matches more than one stage in project '{key}'.");
                continue;
            }

            var stage = matches[0];

            var updated = stage.UpdateStatus(data.Status);
            if (updated.IsFailure)
            {
                row.Failed($"Could not set stage '{data.StageName}' in project '{key}' to {data.Status}: {updated.Error}");
                continue;
            }

            // This import updates rather than creates, so the reported id is the stage the row changed —
            // still the durable link from a line in the file back to the record it touched.
            row.Created(stage.Id);
        }

        return Result.Success();
    }

    private async Task<Dictionary<string, Project>> ResolveProjects(
        ImportPassContext<ImportProjectStageDto> context, CancellationToken cancellationToken)
    {
        // Key is persisted through a value converter, so compare against ProjectKey instances: the
        // property translates but a member of it does not.
        var keys = context.Rows.Select(r => r.Data.ProjectKey).Distinct().ToList();

        return (await _projectPortfolioManagementDbContext.Projects
                .Include(p => p.Stages)
                .Where(p => keys.Contains(p.Key))
                .ToListAsync(cancellationToken))
            .ToDictionary(p => p.Key.Value, p => p, StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string value) => value.Trim();
}
