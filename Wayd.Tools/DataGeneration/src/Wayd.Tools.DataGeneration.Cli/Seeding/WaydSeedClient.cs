using System.Net.Http.Headers;
using System.Text.Json;
using Wayd.Common.Domain.Authorization;
using Wayd.Tools.DataGeneration.Cli.Client;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Seeding;

/// <summary>
/// Talks to the Wayd API for seeding. CSV uploads are posted directly as multipart/form-data (the
/// generated NSwag client mishandles IFormFile, so we do the upload by hand with the field name "file"
/// that the [FromForm] IFormFile endpoints expect). Settings bootstrap reuses the generated typed client.
/// Authentication is a Personal Access Token sent in the x-api-key header on every request.
/// <para>
/// Every import posts and then waits: a submission answers with the id of a queued run, and the ids it
/// created are what the next stage's file references. <see cref="ImportAwaiter"/> owns that wait.
/// </para>
/// <para>
/// One client is one seed: every file it posts is submitted under the same group, so the fifteen or so
/// runs a seed produces can be found together afterwards rather than picked out of everything else the
/// environment has imported.
/// </para>
/// </summary>
public sealed class WaydSeedClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TeamMemberRolesClient _rolesClient;
    private readonly ExpenditureCategoriesClient _expenditureCategoriesClient;
    private readonly ProjectLifecyclesClient _projectLifecyclesClient;
    private readonly RolesClient _applicationRolesClient;
    private readonly UsersClient _usersClient;
    private readonly FeatureFlagsClient _featureFlagsClient;
    private readonly ImportsClient _importsClient;
    private readonly ImportAwaiter _awaiter;

    /// <summary>
    /// How long a stage waits for its run. Generous because the queue is shared: a seed's own earlier
    /// stages, or anything else the environment is doing, sit in front of it.
    /// </summary>
    private static readonly TimeSpan ImportTimeout = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public WaydSeedClient(string baseUrl, string apiKey, Guid submissionGroupId)
    {
        if (submissionGroupId == Guid.Empty)
            throw new ArgumentException("A seed needs a group of its own to submit under.", nameof(submissionGroupId));

        SubmissionGroupId = submissionGroupId;

        var root = baseUrl.TrimEnd('/') + "/";
        _httpClient = new HttpClient { BaseAddress = new Uri(root) };
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

        _rolesClient = new TeamMemberRolesClient(root, _httpClient);
        _expenditureCategoriesClient = new ExpenditureCategoriesClient(root, _httpClient);
        _projectLifecyclesClient = new ProjectLifecyclesClient(root, _httpClient);
        _applicationRolesClient = new RolesClient(root, _httpClient);
        _usersClient = new UsersClient(root, _httpClient);
        _featureFlagsClient = new FeatureFlagsClient(root, _httpClient);
        _importsClient = new ImportsClient(root, _httpClient);
        _awaiter = new ImportAwaiter(_importsClient, PollInterval, ImportTimeout);
    }

    /// <summary>The group every run this client submits is filed under; each run carries it.</summary>
    public Guid SubmissionGroupId { get; }

    /// <summary>
    /// Ensures each application role exists with exactly the permissions given, creating what is missing.
    /// </summary>
    /// <remarks>
    /// Create-or-update by name, so a re-seed onto an environment that already has these roles refreshes
    /// their permissions rather than failing. Admin is skipped if it appears: the API refuses to modify
    /// its permissions, and it already carries everything.
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, string>> EnsureRoles(
        IEnumerable<SeedRole> roles, CancellationToken cancellationToken)
    {
        var existing = await _applicationRolesClient.GetListAsync(cancellationToken);
        var idsByName = existing
            .Where(r => r.Name is not null)
            .ToDictionary(r => r.Name!, r => r.Id!, StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            if (string.Equals(role.Name, ApplicationRoles.Admin, StringComparison.OrdinalIgnoreCase))
                continue;

            var id = await _applicationRolesClient.CreateOrUpdateAsync(
                new CreateOrUpdateRoleRequest
                {
                    Id = idsByName.TryGetValue(role.Name, out var existingId) ? existingId : null,
                    Name = role.Name,
                    Description = role.Description,
                },
                cancellationToken);

            idsByName[role.Name] = id;

            await _applicationRolesClient.UpdatePermissionsAsync(
                id,
                new UpdateRolePermissionsRequest { RoleId = id, Permissions = [.. role.Permissions] },
                cancellationToken);
        }

        return idsByName;
    }

    /// <summary>
    /// Creates one sign-in per generated user, linked to the employee it belongs to.
    /// </summary>
    /// <remarks>
    /// One call each: there is no user import endpoint, and unlike the CSV imports this is a handful of
    /// accounts rather than thousands of rows. A row that fails is reported and the rest continue, because
    /// an environment with most of its accounts is still worth having — the usual cause is an address the
    /// environment already knows, which is not a reason to abandon the run.
    /// </remarks>
    public async Task<int> CreateUsers(
        IEnumerable<GeneratedUser> users,
        IReadOnlyDictionary<string, Guid> employeeIdsByNumber,
        string password,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var created = 0;

        foreach (var user in users)
        {
            if (!employeeIdsByNumber.TryGetValue(user.EmployeeNumber, out var employeeId))
            {
                log($"  skipped {user.Email}: no employee was created for {user.EmployeeNumber}.");
                continue;
            }

            try
            {
                await _usersClient.CreateUserAsync(
                    new CreateUserRequest
                    {
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        EmployeeId = employeeId,
                        LoginProvider = LocalLoginProvider,
                        Password = password,
                        RoleNames = [user.RoleName],
                    },
                    cancellationToken);

                created++;
            }
            catch (WaydApiException ex)
            {
                log($"  skipped {user.Email}: {ex.Message}");
            }
        }

        return created;
    }

    /// <summary>Reads the row cap of every import type this token can see.</summary>
    public async Task<ImportLimits> GetImportLimits(CancellationToken cancellationToken) =>
        new(await _importsClient.GetDefinitionsAsync(cancellationToken));

    /// <summary>The provider a password-backed account signs in through.</summary>
    private const string LocalLoginProvider = "Wayd";

    public Task<ImportRun> ImportEmployees(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/organization/employees/import", csv, "employees.csv", "employees", cancellationToken);

    public Task<ImportRun> ImportTeams(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/organization/teams/import", csv, "teams.csv", "teams", cancellationToken);

    public Task<ImportRun> ImportTeamMemberships(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/organization/teams/team-memberships/import", csv, "team-memberships.csv", "team hierarchy", cancellationToken);

    public Task<ImportRun> ImportTeamMembers(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/organization/teams/members/import", csv, "members.csv", "team staffing", cancellationToken);

    // ---- PPM CSV imports ----------------------------------------------------------------------

    public Task<ImportRun> ImportStrategicThemes(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/strategic-management/strategic-themes/import", csv, "strategic-themes.csv", "strategic themes", cancellationToken);

    public Task<ImportRun> ImportPortfolios(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/ppm/portfolios/import", csv, "portfolios.csv", "portfolios", cancellationToken);

    public Task<ImportRun> ImportPrograms(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/ppm/programs/import", csv, "programs.csv", "programs", cancellationToken);

    public Task<ImportRun> ImportProjects(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/ppm/projects/import", csv, "projects.csv", "projects", cancellationToken);

    public Task<ImportRun> ImportProjectTasks(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/ppm/projects/tasks/import", csv, "project-tasks.csv", "project tasks", cancellationToken);

    public Task<ImportRun> ImportProjectStages(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/ppm/projects/stages/import", csv, "project-stages.csv", "project stage statuses", cancellationToken);

    public Task<ImportRun> ImportStrategicInitiatives(byte[] initiativesCsv, byte[]? kpisCsv, CancellationToken cancellationToken) =>
        Import("api/ppm/strategic-initiatives/import", initiativesCsv, "strategic-initiatives.csv", "strategic initiatives", cancellationToken,
            secondFieldName: "kpiFile", secondCsv: kpisCsv, secondFileName: "strategic-initiative-kpis.csv");

    public Task<ImportRun> ImportPpmFinalizations(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/ppm/portfolios/finalize/import", csv, "ppm-finalizations.csv", "finalize", cancellationToken);

    // ---- Product Management CSV imports -------------------------------------------------------

    public Task<ImportRun> ImportDeploymentEnvironments(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/product-management/deployment-environments/import", csv, "deployment-environments.csv", "deployment environments", cancellationToken);

    public Task<ImportRun> ImportProducts(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/product-management/products/import", csv, "products.csv", "products", cancellationToken);

    public Task<ImportRun> ImportVersions(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/product-management/versions/import", csv, "versions.csv", "versions", cancellationToken);

    public Task<ImportRun> ImportReleasePackages(byte[] packagesCsv, byte[] manifestCsv, CancellationToken cancellationToken) =>
        Import("api/product-management/release-packages/import", packagesCsv, "release-packages.csv", "release packages", cancellationToken,
            secondFieldName: "manifestFile", secondCsv: manifestCsv, secondFileName: "release-package-components.csv");

    public Task<ImportRun> ImportReleases(byte[] releasesCsv, byte[]? contentsCsv, CancellationToken cancellationToken) =>
        Import("api/product-management/releases/import", releasesCsv, "releases.csv", "releases", cancellationToken,
            secondFieldName: "contentsFile", secondCsv: contentsCsv, secondFileName: "release-contents.csv");

    public Task<ImportRun> ImportDeployments(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/product-management/deployments/import", csv, "deployments.csv", "deployments", cancellationToken);

    // ---- Planning CSV imports -----------------------------------------------------------------

    public Task<ImportRun> ImportPlanningIntervals(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/planning/planning-intervals/import", csv, "planning-intervals.csv", "planning intervals", cancellationToken);

    public Task<ImportRun> ImportPlanningIntervalObjectives(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/planning/planning-intervals/objectives/import", csv, "planning-interval-objectives.csv", "planning interval objectives", cancellationToken);

    public Task<ImportRun> ImportRisks(byte[] csv, CancellationToken cancellationToken) =>
        Import("api/planning/risks/import", csv, "risks.csv", "risks", cancellationToken);

    /// <summary>
    /// Switches a feature flag on, answering whether it had to be.
    /// </summary>
    /// <remarks>
    /// Product Management is seeded disabled, and every one of its endpoints — the imports included —
    /// answers 404 until it is enabled. A seed asked for that data is asking for the module.
    /// </remarks>
    public async Task<bool> EnsureFeatureFlagEnabled(string name, CancellationToken cancellationToken)
    {
        var flags = await _featureFlagsClient.FeatureFlagsAsync(includeArchived: false, cancellationToken);
        var flag = flags.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new SeedException($"The environment has no '{name}' feature flag. Is the API older than this tool?");

        if (flag.IsEnabled)
            return false;

        await _featureFlagsClient.ToggleAsync(flag.Id, new ToggleFeatureFlagRequest { Id = flag.Id, IsEnabled = true }, cancellationToken);
        return true;
    }

    // ---- Settings bootstrap (create-or-get by name) -------------------------------------------

    /// <summary>
    /// Ensures each expenditure category exists, creating any that are missing, and answers with their ids
    /// keyed by name.
    /// </summary>
    /// <remarks>
    /// Categories are settings-level rather than an import, and projects reference them by id — so the ids
    /// have to come back here, since nothing downstream can look them up from a name.
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, int>> EnsureExpenditureCategories(
        IEnumerable<PpmVocabulary.ExpenditureCategoryDefinition> categories, CancellationToken cancellationToken)
    {
        var existing = await _expenditureCategoriesClient.GetExpenditureCategoriesAsync(cancellationToken);
        var byName = existing.ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var category in categories)
        {
            if (byName.ContainsKey(category.Name))
                continue;

            byName[category.Name] = await _expenditureCategoriesClient.CreateAsync(new CreateExpenditureCategoryRequest
            {
                Name = category.Name,
                Description = category.Description,
                IsCapitalizable = category.IsCapitalizable,
                RequiresDepreciation = category.RequiresDepreciation,
                AccountingCode = category.AccountingCode,
            }, cancellationToken);
        }

        return byName;
    }

    /// <summary>
    /// Ensures the project lifecycle exists (creating it with its stages if missing), returns it active
    /// since a lifecycle must be active before a project can be assigned it, and answers with its id.
    /// </summary>
    public async Task<Guid> EnsureProjectLifecycle(PpmVocabulary.ProjectLifecycleDefinition lifecycle, CancellationToken cancellationToken)
    {
        var existing = await _projectLifecyclesClient.GetProjectLifecyclesAsync(null, cancellationToken);
        var match = existing.FirstOrDefault(l => string.Equals(l.Name, lifecycle.Name, StringComparison.OrdinalIgnoreCase));

        // If it already exists and is active, there is nothing to do — projects can use it as-is.
        if (match is not null && string.Equals(match.State?.Name, "Active", StringComparison.OrdinalIgnoreCase))
            return match.Id;

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
                Stages = [.. lifecycle.Stages.Select(p => new StageInput { Name = p.Name, Description = p.Description })],
            }, cancellationToken);
        }

        // Activate the draft so projects can be assigned it. Any failure here is surfaced rather than
        // swallowed — a lifecycle that cannot be activated (a real error, an archived one, an auth problem)
        // would otherwise let the whole seed proceed and fail confusingly at project import.
        await _projectLifecyclesClient.ActivateAsync(lifecycleId, cancellationToken);

        return lifecycleId;
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

    /// <summary>Posts a file, waits for the run it queued, and answers with what that run created.</summary>
    private async Task<ImportRun> Import(
        string path, byte[] csv, string fileName, string label, CancellationToken cancellationToken,
        string? secondFieldName = null, byte[]? secondCsv = null, string? secondFileName = null)
    {
        var processId = await PostCsv(path, csv, fileName, cancellationToken, secondFieldName, secondCsv, secondFileName);

        return await _awaiter.Await(processId, label, cancellationToken);
    }

    /// <summary>Posts the multipart file(s) and reads the run id out of the run the endpoint answers with.</summary>
    private async Task<Guid> PostCsv(string path, byte[] csv, string fileName, CancellationToken cancellationToken,
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

        // The group rides on the query string: the endpoints take it there so the multipart body stays
        // the one "file" field their generated clients describe.
        using var response = await _httpClient.PostAsync(
            $"{path}?submissionGroupId={SubmissionGroupId}", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new SeedException($"POST {path} failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");

        // The body is the run — finished (200) or still in flight (202). Only its id is read here: the awaiter
        // reads the run back either way, so a run that already finished simply ends on the first poll. A body
        // without one means the endpoint is not the kind this tool expects, which is worth saying rather than
        // parsing past.
        var processId = TryReadRunId(body);
        if (processId is null || processId == Guid.Empty)
            throw new SeedException($"POST {path} did not answer with an import run. Body: {body}");

        return processId.Value;
    }

    private static Guid? TryReadRunId(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("id", out var id)
                && id.TryGetGuid(out var runId)
                    ? runId
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Dispose() => _httpClient.Dispose();
}

public sealed class SeedException(string message) : Exception(message);
