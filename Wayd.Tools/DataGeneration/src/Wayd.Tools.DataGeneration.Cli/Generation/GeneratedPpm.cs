using Wayd.Tools.DataGeneration.Cli.Csv;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The generated PPM dataset, as the CSV row sets the API import endpoints consume. Expenditure categories
/// and lifecycles are not CSVs — they are bootstrapped through the settings API and carried here as the
/// definitions to create, since the projects reference them by name.
/// </summary>
public sealed record GeneratedPpm(
    IReadOnlyList<StrategicThemeCsvRow> StrategicThemes,
    IReadOnlyList<PortfolioCsvRow> Portfolios,
    IReadOnlyList<ProgramCsvRow> Programs,
    IReadOnlyList<ProjectCsvRow> Projects,
    IReadOnlyList<ProjectTaskCsvRow> ProjectTasks,
    IReadOnlyList<ProjectPhaseCsvRow> ProjectPhases,
    IReadOnlyList<StrategicInitiativeCsvRow> StrategicInitiatives,
    IReadOnlyList<StrategicInitiativeKpiCsvRow> StrategicInitiativeKpis,
    IReadOnlyList<PpmFinalizationCsvRow> Finalizations,
    IReadOnlyList<PpmVocabulary.ExpenditureCategoryDefinition> ExpenditureCategories,
    PpmVocabulary.ProjectLifecycleDefinition Lifecycle);
