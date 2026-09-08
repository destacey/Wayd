using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Recipes;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// One resolved recipe, generated: the organization and, unless the recipe switched it off, the PPM
/// dataset over it.
/// </summary>
/// <remarks>
/// Exists so the two front ends produce the same thing rather than each assembling the generators for
/// itself. The CLI's <c>generate</c> verb and the UI's preview both go through here, which is what makes
/// "the page shows you the command that would do this" a true statement rather than an intention.
/// </remarks>
public sealed record GeneratedDataset(GeneratedOrg Org, GeneratedPpm? Ppm)
{
    /// <summary>Runs the generators for a resolved recipe.</summary>
    public static GeneratedDataset From(ResolvedRecipe resolved)
    {
        var org = new OrgGenerator(resolved.Organization, resolved.Context).Generate();
        var ppm = resolved.GeneratePpm
            ? new PpmGenerator(org.Structure, resolved.Ppm, resolved.Context).Generate()
            : null;

        return new GeneratedDataset(org, ppm);
    }

    /// <summary>
    /// Writes every file to a directory.
    /// </summary>
    /// <remarks>
    /// The organization files are postable as they stand — every reference in them is a natural key the
    /// generator owns. The PPM files are the generated model, naming portfolios and categories rather than
    /// pointing at ids, because those ids only exist once a run has created the records. They are for
    /// reading; <c>seed</c> resolves them stage by stage as it goes.
    /// </remarks>
    public void WriteTo(string directory)
    {
        Directory.CreateDirectory(directory);

        CsvFile.Write(Path.Combine(directory, "employees.csv"), Org.Employees);
        CsvFile.Write(Path.Combine(directory, "teams.csv"), Org.Teams);
        CsvFile.Write(Path.Combine(directory, "team-memberships.csv"), Org.TeamMemberships);
        CsvFile.Write(Path.Combine(directory, "members.csv"), Org.Members);

        if (Ppm is not { } ppm)
            return;

        CsvFile.Write(Path.Combine(directory, "strategic-themes.csv"), ppm.StrategicThemes);
        CsvFile.Write(Path.Combine(directory, "portfolios.csv"), ppm.Portfolios);
        CsvFile.Write(Path.Combine(directory, "programs.csv"), ppm.Programs);
        CsvFile.Write(Path.Combine(directory, "projects.csv"), ppm.Projects);
        CsvFile.Write(Path.Combine(directory, "project-tasks.csv"), ppm.ProjectTasks);
        CsvFile.Write(Path.Combine(directory, "project-stages.csv"), ppm.ProjectStages);
        CsvFile.Write(Path.Combine(directory, "strategic-initiatives.csv"), ppm.StrategicInitiatives);
        CsvFile.Write(Path.Combine(directory, "strategic-initiative-kpis.csv"), ppm.StrategicInitiativeKpis);
        CsvFile.Write(Path.Combine(directory, "ppm-finalizations.csv"), ppm.Finalizations);
    }

    /// <summary>What was generated, for a run summary or a preview.</summary>
    public DatasetCounts Counts => new(
        Org.Employees.Count,
        Org.Teams.Count,
        Org.TeamMemberships.Count,
        Org.Members.Count,
        Ppm?.Portfolios.Count ?? 0,
        Ppm?.Programs.Count ?? 0,
        Ppm?.Projects.Count ?? 0,
        Ppm?.ProjectTasks.Count ?? 0,
        Ppm?.StrategicInitiatives.Count ?? 0);
}

/// <summary>How much of each thing a run produced.</summary>
public sealed record DatasetCounts(
    int Employees,
    int Teams,
    int HierarchyLinks,
    int Staffing,
    int Portfolios,
    int Programs,
    int Projects,
    int Tasks,
    int Initiatives);
