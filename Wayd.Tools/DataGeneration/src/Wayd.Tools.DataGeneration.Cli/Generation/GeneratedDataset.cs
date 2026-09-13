using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Recipes;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// One resolved recipe, generated: the organization and, unless the recipe switched them off, the PPM and
/// Product Management datasets over it.
/// </summary>
/// <remarks>
/// Exists so the two front ends produce the same thing rather than each assembling the generators for
/// itself. The CLI's <c>generate</c> verb and the UI's preview both go through here, which is what makes
/// "the page shows you the command that would do this" a true statement rather than an intention.
/// </remarks>
public sealed record GeneratedDataset(GeneratedOrg Org, GeneratedPpm? Ppm, GeneratedProductManagement? ProductManagement)
{
    /// <summary>Runs the generators for a resolved recipe.</summary>
    public static GeneratedDataset From(ResolvedRecipe resolved)
    {
        var org = new OrgGenerator(resolved.Organization, resolved.Context).Generate();
        var ppm = resolved.GeneratePpm
            ? new PpmGenerator(org.Structure, resolved.Ppm, resolved.Context).Generate()
            : null;
        var productManagement = resolved.GenerateProductManagement
            ? new ProductManagementGenerator(org.Structure, resolved.ProductManagement, resolved.Context).Generate()
            : null;

        return new GeneratedDataset(org, ppm, productManagement);
    }

    /// <summary>
    /// Writes every file to a directory.
    /// </summary>
    /// <remarks>
    /// The organization files are postable as they stand — every reference in them is a natural key the
    /// generator owns. The PPM and Product Management files are the generated model, naming the records
    /// they point at rather than carrying ids, because those ids only exist once a run has created the
    /// records. They are for reading; <c>seed</c> resolves them stage by stage as it goes.
    /// </remarks>
    public void WriteTo(string directory)
    {
        Directory.CreateDirectory(directory);

        CsvFile.Write(Path.Combine(directory, "employees.csv"), Org.Employees);
        CsvFile.Write(Path.Combine(directory, "teams.csv"), Org.Teams);
        CsvFile.Write(Path.Combine(directory, "team-memberships.csv"), Org.TeamMemberships);
        CsvFile.Write(Path.Combine(directory, "members.csv"), Org.Members);

        if (Ppm is { } ppm)
        {
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

        if (ProductManagement is { } pm)
        {
            CsvFile.Write(Path.Combine(directory, "deployment-environments.csv"), pm.Environments);
            CsvFile.Write(Path.Combine(directory, "products.csv"), pm.Products);
            CsvFile.Write(Path.Combine(directory, "versions.csv"), pm.Versions);
            CsvFile.Write(Path.Combine(directory, "release-packages.csv"), pm.ReleasePackages);
            CsvFile.Write(Path.Combine(directory, "release-package-components.csv"), pm.ReleasePackageComponents);
            CsvFile.Write(Path.Combine(directory, "releases.csv"), pm.Releases);
            CsvFile.Write(Path.Combine(directory, "release-contents.csv"), pm.ReleaseContents);
            CsvFile.Write(Path.Combine(directory, "deployments.csv"), pm.Deployments);
        }
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
        Ppm?.StrategicInitiatives.Count ?? 0,
        ProductManagement?.Products.Count ?? 0,
        ProductManagement?.Versions.Count ?? 0,
        ProductManagement?.ReleasePackages.Count ?? 0,
        ProductManagement?.Releases.Count ?? 0,
        ProductManagement?.Deployments.Count ?? 0);
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
    int Initiatives,
    int Products,
    int Versions,
    int ReleasePackages,
    int Releases,
    int Deployments);
