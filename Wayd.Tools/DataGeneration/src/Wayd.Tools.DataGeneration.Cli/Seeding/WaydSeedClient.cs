using System.Net.Http.Headers;
using Wayd.Tools.DataGeneration.Cli.Client;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Seeding;

/// <summary>
/// Talks to the Wayd API for seeding. CSV uploads are posted directly as multipart/form-data (the
/// generated NSwag client mishandles IFormFile, so we do the upload by hand with the field name "file"
/// that the [FromForm] IFormFile endpoints expect). Role bootstrap reuses the generated typed client.
/// Authentication is a Personal Access Token sent in the x-api-key header on every request.
/// </summary>
public sealed class WaydSeedClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TeamMemberRolesClient _rolesClient;
    private readonly ExpenditureCategoriesClient _expenditureCategoriesClient;
    private readonly ProjectLifecyclesClient _projectLifecyclesClient;

    public WaydSeedClient(string baseUrl, string apiKey)
    {
        var root = baseUrl.TrimEnd('/') + "/";
        _httpClient = new HttpClient { BaseAddress = new Uri(root) };
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

        _rolesClient = new TeamMemberRolesClient(root, _httpClient);
        _expenditureCategoriesClient = new ExpenditureCategoriesClient(root, _httpClient);
        _projectLifecyclesClient = new ProjectLifecyclesClient(root, _httpClient);
    }

    public Task ImportEmployees(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/organization/employees/import", csv, "employees.csv", cancellationToken);

    public Task ImportTeams(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/organization/teams/import", csv, "teams.csv", cancellationToken);

    public Task ImportTeamMemberships(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/organization/teams/team-memberships/import", csv, "team-memberships.csv", cancellationToken);

    public Task ImportTeamMembers(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/organization/teams/members/import", csv, "members.csv", cancellationToken);

    // ---- PPM CSV imports ----------------------------------------------------------------------

    public Task ImportStrategicThemes(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/strategic-management/strategic-themes/import", csv, "strategic-themes.csv", cancellationToken);

    public Task ImportPortfolios(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/ppm/portfolios/import", csv, "portfolios.csv", cancellationToken);

    public Task ImportPrograms(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/ppm/programs/import", csv, "programs.csv", cancellationToken);

    public Task ImportProjects(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/ppm/projects/import", csv, "projects.csv", cancellationToken);

    public Task ImportProjectTasks(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/ppm/projects/tasks/import", csv, "project-tasks.csv", cancellationToken);

    public Task ImportProjectPhases(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/ppm/projects/phases/import", csv, "project-phases.csv", cancellationToken);

    public Task ImportStrategicInitiatives(byte[] initiativesCsv, byte[]? kpisCsv, CancellationToken cancellationToken) =>
        PostCsv("api/ppm/strategic-initiatives/import", initiativesCsv, "strategic-initiatives.csv", cancellationToken,
            secondFieldName: "kpiFile", secondCsv: kpisCsv, secondFileName: "strategic-initiative-kpis.csv");

    public Task ImportPpmFinalizations(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/ppm/portfolios/finalize/import", csv, "ppm-finalizations.csv", cancellationToken);

    // ---- Settings bootstrap (create-or-get by name) -------------------------------------------

    /// <summary>
    /// Ensures each expenditure category exists, creating any that are missing. Categories are settings-level
    /// (no CSV import); projects reference them by name, so this must run before the project import.
    /// </summary>
    public async Task EnsureExpenditureCategories(IEnumerable<PpmVocabulary.ExpenditureCategoryDefinition> categories, CancellationToken cancellationToken)
    {
        var existing = await _expenditureCategoriesClient.GetExpenditureCategoriesAsync(cancellationToken);
        var existingNames = existing.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var category in categories)
        {
            if (existingNames.Contains(category.Name))
                continue;

            await _expenditureCategoriesClient.CreateAsync(new CreateExpenditureCategoryRequest
            {
                Name = category.Name,
                Description = category.Description,
                IsCapitalizable = category.IsCapitalizable,
                RequiresDepreciation = category.RequiresDepreciation,
                AccountingCode = category.AccountingCode,
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Ensures the project lifecycle exists (creating it with its phases if missing) and returns it active,
    /// since a lifecycle must be active before a project can be assigned it. Projects reference it by name.
    /// </summary>
    public async Task EnsureProjectLifecycle(PpmVocabulary.ProjectLifecycleDefinition lifecycle, CancellationToken cancellationToken)
    {
        var existing = await _projectLifecyclesClient.GetProjectLifecyclesAsync(null, cancellationToken);
        var match = existing.FirstOrDefault(l => string.Equals(l.Name, lifecycle.Name, StringComparison.OrdinalIgnoreCase));

        // If it already exists and is active, there is nothing to do — projects can use it as-is.
        if (match is not null && string.Equals(match.State?.Name, "Active", StringComparison.OrdinalIgnoreCase))
            return;

        Guid lifecycleId;
        if (match is not null)
        {
            lifecycleId = match.Id;
        }
        else
        {
            lifecycleId = await _projectLifecyclesClient.CreateAsync(new CreateProjectLifecycleRequest
            {
                Name = lifecycle.Name,
                Description = lifecycle.Description,
                Phases = [.. lifecycle.Phases.Select(p => new PhaseInput { Name = p.Name, Description = p.Description })],
            }, cancellationToken);
        }

        // Activate the draft so projects can be assigned it. Any failure here is surfaced rather than
        // swallowed — a lifecycle that cannot be activated (a real error, an archived one, an auth problem)
        // would otherwise let the whole seed proceed and fail confusingly at project import.
        await _projectLifecyclesClient.ActivateAsync(lifecycleId, cancellationToken);
    }

    /// <summary>
    /// Ensures every named role exists, creating any that are missing. Roles are not reference-seeded, so
    /// this must run before staffing. Returns after all named roles are present.
    /// </summary>
    public async Task EnsureRoles(IReadOnlyList<string> roleNames, CancellationToken cancellationToken)
    {
        var existing = await _rolesClient.GetListAsync(includeInactive: true, cancellationToken);
        var existingNames = existing
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in roleNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (existingNames.Contains(roleName))
                continue;

            await _rolesClient.CreateAsync(new CreateTeamMemberRoleRequest { Name = roleName }, cancellationToken);
        }
    }

    private async Task PostCsv(string path, byte[] csv, string fileName, CancellationToken cancellationToken,
        string? secondFieldName = null, byte[]? secondCsv = null, string? secondFileName = null)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(csv);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        // The field name must be "file" to bind to the endpoints' [FromForm] IFormFile file parameter.
        content.Add(fileContent, "file", fileName);

        // Some endpoints (strategic initiatives) take an optional second file — e.g. the KPIs alongside the
        // initiatives, in one call so the KPIs land before each initiative is driven to its final status.
        if (secondCsv is { Length: > 0 } && secondFieldName is not null)
        {
            var secondContent = new ByteArrayContent(secondCsv);
            secondContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(secondContent, secondFieldName, secondFileName ?? "second.csv");
        }

        using var response = await _httpClient.PostAsync(path, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new SeedException($"POST {path} failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");
        }
    }

    public void Dispose() => _httpClient.Dispose();
}

public sealed class SeedException(string message) : Exception(message);
