namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The generated PPM dataset, keyed throughout by the generator's own handles. Each seed area projects its
/// slice to CSV rows, substituting the ids the environment has handed back by then.
/// </summary>
/// <remarks>
/// Expenditure categories and the lifecycle are not imports — they are bootstrapped through the settings
/// API and carried here as the definitions to create, since a project needs their ids.
/// </remarks>
public sealed record GeneratedPpm(
    IReadOnlyList<StrategicThemeModel> StrategicThemes,
    IReadOnlyList<PortfolioModel> Portfolios,
    IReadOnlyList<ProgramModel> Programs,
    IReadOnlyList<ProjectModel> Projects,
    IReadOnlyList<ProjectTaskModel> ProjectTasks,
    IReadOnlyList<ProjectStageModel> ProjectStages,
    IReadOnlyList<StrategicInitiativeModel> StrategicInitiatives,
    IReadOnlyList<StrategicInitiativeKpiModel> StrategicInitiativeKpis,
    IReadOnlyList<PpmFinalizationModel> Finalizations,
    IReadOnlyList<PpmVocabulary.ExpenditureCategoryDefinition> ExpenditureCategories,
    PpmVocabulary.ProjectLifecycleDefinition Lifecycle);
