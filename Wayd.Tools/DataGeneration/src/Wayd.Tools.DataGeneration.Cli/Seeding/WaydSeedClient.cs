using System.Net.Http.Headers;
using Wayd.Tools.DataGeneration.Cli.Client;

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

    public WaydSeedClient(string baseUrl, string apiKey)
    {
        var root = baseUrl.TrimEnd('/') + "/";
        _httpClient = new HttpClient { BaseAddress = new Uri(root) };
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

        _rolesClient = new TeamMemberRolesClient(root, _httpClient);
    }

    public Task ImportEmployees(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/organization/employees/import", csv, "employees.csv", cancellationToken);

    public Task ImportTeams(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/organization/teams/import", csv, "teams.csv", cancellationToken);

    public Task ImportTeamMemberships(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/organization/teams/team-memberships/import", csv, "team-memberships.csv", cancellationToken);

    public Task ImportTeamMembers(byte[] csv, CancellationToken cancellationToken) =>
        PostCsv("api/organization/teams/members/import", csv, "members.csv", cancellationToken);

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

    private async Task PostCsv(string path, byte[] csv, string fileName, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(csv);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        // The field name must be "file" to bind to the endpoints' [FromForm] IFormFile file parameter.
        content.Add(fileContent, "file", fileName);

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
