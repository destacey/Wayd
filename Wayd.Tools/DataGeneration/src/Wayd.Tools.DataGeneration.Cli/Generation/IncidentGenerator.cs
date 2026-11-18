using static Wayd.Tools.DataGeneration.Cli.Generation.ProductManagementVocabulary;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// A generated incident, named by the product, release and employee the catalog already created.
/// Ids are substituted when the row is posted.
/// </summary>
public sealed class IncidentModel
{
    public required string ImportId { get; init; }
    public required string Title { get; init; }
    public string? Symptoms { get; init; }
    public string? RootCause { get; init; }
    public string? Remediation { get; init; }

    /// <summary>A level name in the system SEV set a new database selects.</summary>
    public required string Severity { get; init; }

    /// <summary><c>Open</c>, <c>Responding</c>, <c>Resolved</c>, <c>Closed</c>, or <c>Canceled</c>.</summary>
    public required string Status { get; init; }

    public required string CommanderEmployeeNumber { get; init; }
    public required string ProductName { get; init; }
    public required string ReleaseVersion { get; init; }
    public required DateTimeOffset NoticedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? RecoveredAt { get; init; }
}

/// <summary>
/// One incident for every status at every level of the system SEV set, each pointing at one component
/// of a product line's newest released release and commanded by someone still on that component's team.
/// </summary>
/// <remarks>
/// Draws nothing from the catalog's random stream, so the products, releases and history are the same
/// whether or not these rows are built. Twenty is the grid itself — five statuses by four severities —
/// and not a count anyone sets.
/// </remarks>
public sealed class IncidentGenerator(
    OrgStructure org,
    IReadOnlyList<ProductModel> products,
    IReadOnlyList<ReleaseModel> releases,
    IReadOnlyList<ReleaseContentModel> contents,
    IReadOnlyList<ReleasePackageComponentModel> packageComponents,
    DateOnly asOf)
{
    private static readonly string[] Statuses = ["Open", "Responding", "Resolved", "Closed", "Canceled"];
    private static readonly string[] Severities = ["SEV-1", "SEV-2", "SEV-3", "SEV-4"];

    private readonly Dictionary<string, TeamNode> _teams = org.ValueStreams
        .SelectMany(stream => stream.Arts)
        .SelectMany(art => art.Teams)
        .ToDictionary(team => team.TeamCode, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IncidentModel> Generate()
    {
        var subjects = SelectSubjects();
        if (subjects.Count == 0)
            return [];

        var incidents = new List<IncidentModel>(Statuses.Length * Severities.Length);
        var index = 0;
        foreach (var status in Statuses)
        {
            foreach (var severity in Severities)
            {
                incidents.Add(Create(subjects[index % subjects.Count], status, severity, index));
                index++;
            }
        }

        return incidents;
    }

    /// <summary>
    /// One component per product line: the newest release that has already shipped and still contains a
    /// staffed component, and the first such component on it.
    /// </summary>
    private List<Subject> SelectSubjects()
    {
        var byName = products.ToDictionary(product => product.Name, StringComparer.OrdinalIgnoreCase);
        var released = releases
            .Where(release => release.ProductName is not null && release.ReleasedDate is { } on && on <= asOf)
            .OrderByDescending(release => release.ReleasedDate)
            .ThenBy(release => release.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var subjects = new List<Subject>();
        foreach (var line in released.GroupBy(release => release.ProductName!, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var release in line)
            {
                var component = EligibleComponent(release, byName);
                if (component is null)
                    continue;

                subjects.Add(new Subject(component, release, Commander(component)!));
                break;
            }
        }

        return subjects;
    }

    private ProductModel? EligibleComponent(ReleaseModel release, Dictionary<string, ProductModel> byName)
    {
        return ComponentNames(release)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => byName.GetValueOrDefault(name))
            .Where(product => product is not null)
            .Select(product => product!)
            .Where(product => product.ProductTypeName is not (ProductLineType or ProductType))
            .Where(product => product.Status is ActiveStatus or SunsetStatus)
            .Where(product => Commander(product) is not null)
            .OrderBy(product => product.Status == ActiveStatus ? 0 : 1)
            .ThenBy(product => product.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private IEnumerable<string> ComponentNames(ReleaseModel release)
    {
        foreach (var content in contents.Where(content =>
            string.Equals(content.ReleaseVersion, release.Version, StringComparison.OrdinalIgnoreCase)))
        {
            if (content.VersionHandle is { } handle)
            {
                var split = handle.Split('|', 2);
                if (split.Length == 2)
                    yield return split[0];
            }

            if (content.PackageVersion is not { } package)
                continue;

            foreach (var component in packageComponents.Where(component =>
                string.Equals(component.PackageVersion, package, StringComparison.OrdinalIgnoreCase)))
            {
                yield return component.ProductName;
            }
        }
    }

    /// <summary>
    /// A person still employed on one of the component's teams, preferring its engineering manager.
    /// Team membership is current state, and a leaver holds none.
    /// </summary>
    private string? Commander(ProductModel product)
    {
        if (string.IsNullOrEmpty(product.Teams))
            return null;

        foreach (var code in product.Teams.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!_teams.TryGetValue(code, out var team))
                continue;

            var number = team.EngineeringManagerEmployeeNumber;
            if (number is not null && IsCurrent(number) && team.MemberEmployeeNumbers.Contains(number, StringComparer.OrdinalIgnoreCase))
                return number;

            var member = team.MemberEmployeeNumbers.FirstOrDefault(IsCurrent);
            if (member is not null)
                return member;
        }

        return null;
    }

    private bool IsCurrent(string employeeNumber)
    {
        var tenure = org.TenureOf(employeeNumber);
        return tenure is null || tenure.LeftOn is null;
    }

    private IncidentModel Create(Subject subject, string status, string severity, int index)
    {
        // Each incident is noticed on its own day, all of them before the day the catalog is generated as of.
        var noticed = new DateTimeOffset(asOf.AddDays(-(index + 1)), new TimeOnly(9, 0), TimeSpan.Zero).AddMinutes(index);
        var started = HasResponse(status) ? noticed.AddHours(2) : (DateTimeOffset?)null;
        var recovered = HasRecovery(status) ? started!.Value.AddHours(4) : (DateTimeOffset?)null;

        return new IncidentModel
        {
            ImportId = $"{status}|{severity}|{subject.Product.Name}",
            Title = Title(subject.Product.Name, status, severity),
            Symptoms = Symptoms(status),
            RootCause = HasRecovery(status) ? "A bad configuration reached production." : null,
            Remediation = HasRecovery(status) ? "The last good configuration was restored." : null,
            Severity = severity,
            Status = status,
            CommanderEmployeeNumber = subject.Commander,
            ProductName = subject.Product.Name,
            ReleaseVersion = subject.Release.Version,
            NoticedAt = noticed,
            StartedAt = started,
            RecoveredAt = recovered,
        };
    }

    private static bool HasResponse(string status) => status is "Responding" or "Resolved" or "Closed";

    private static bool HasRecovery(string status) => status is "Resolved" or "Closed";

    private static string Title(string product, string status, string severity) => status switch
    {
        "Open" => $"{severity}: {product} is down",
        "Responding" => $"{severity}: responding to {product}",
        "Resolved" => $"{severity}: {product} recovered",
        "Closed" => $"{severity}: {product} outage closed",
        _ => $"{severity}: {product} report canceled",
    };

    private static string Symptoms(string status) => status switch
    {
        "Open" => "Customers cannot reach the service.",
        "Responding" => "Error rates are elevated and a responder is on it.",
        "Resolved" => "The service is taking traffic again.",
        "Closed" => "Service was restored and the follow-up is done.",
        _ => "The report did not match a real outage.",
    };

    private sealed record Subject(ProductModel Product, ReleaseModel Release, string Commander);
}
