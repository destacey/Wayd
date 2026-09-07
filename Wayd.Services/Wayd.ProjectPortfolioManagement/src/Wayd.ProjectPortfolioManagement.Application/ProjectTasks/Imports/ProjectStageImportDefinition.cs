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
/// Atomic, matching the single save the command it replaces did. A stage import is a correction pass over
/// projects that already exist, so a file that half applies leaves a project's stages disagreeing with each
/// other with no record of which half landed.
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

    public override ImportAtomicity Atomicity => ImportAtomicity.Atomic;

    // An atomic import cannot be split, so the row cap is what actually bounds one run.
    public override int MaxRows => 10_000;

    protected override IReadOnlyList<ImportPass<ImportProjectStageDto>> Steps =>
    [
        new("UpdateStages", ImportPassScope.WholeSet, UpdateStages),
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
