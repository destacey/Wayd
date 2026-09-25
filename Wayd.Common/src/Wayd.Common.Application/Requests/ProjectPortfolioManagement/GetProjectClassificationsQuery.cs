namespace Wayd.Common.Application.Requests.ProjectPortfolioManagement;

/// <summary>
/// Where each of <paramref name="ProjectIds"/> sits in the portfolio and which strategic themes it serves.
/// Ids of projects that no longer exist are left out.
/// </summary>
public sealed record GetProjectClassificationsQuery(IReadOnlyCollection<Guid> ProjectIds) : IQuery<List<ProjectClassification>>;

/// <param name="Themes">
/// The project's effective themes: its own when it has any, otherwise its program's. The two sets replace
/// each other rather than combining, so work on the project is never spread across both levels' themes.
/// </param>
/// <param name="ThemesFromProgram">True when <paramref name="Themes"/> were inherited from the program.</param>
public sealed record ProjectClassification(
    Guid ProjectId,
    string ProjectKey,
    string ProjectName,
    PpmRecordReference Portfolio,
    PpmRecordReference? Program,
    IReadOnlyList<PpmRecordReference> Themes,
    bool ThemesFromProgram);

public sealed record PpmRecordReference(Guid Id, int Key, string Name);
