using System.Text;

namespace Wayd.Tools.DataGeneration.Cli.Generation;

/// <summary>
/// The <c>Links</c> cell a generated record is imported with: none, one or two links named for the kind of record,
/// on <c>example.com</c> addresses.
/// </summary>
/// <remarks>
/// Chosen from a hash of the record's natural key rather than from the generator's random source, so adding links
/// draws nothing from it: every other generated value stays what it was for the same seed, and the same record always
/// gets the same links.
/// </remarks>
public static class SeedLinks
{
    /// <summary>What each kind of record links to: a name and the path segment its address is filed under.</summary>
    private static readonly Dictionary<SeedLinkKind, (string Name, string Path)[]> _catalog = new()
    {
        [SeedLinkKind.Team] = [("Team wiki", "wiki/teams"), ("Board", "boards")],
        [SeedLinkKind.PlanningInterval] = [("Planning board", "planning"), ("Program increment notes", "wiki/planning")],
        [SeedLinkKind.Objective] = [("Epic", "work/epics"), ("Demo recording", "media/demos")],
        [SeedLinkKind.Risk] = [("Mitigation plan", "wiki/risks"), ("Risk review", "reviews/risks")],
        [SeedLinkKind.Portfolio] = [("Strategy deck", "docs/strategy"), ("Investment review", "reviews/investment")],
        [SeedLinkKind.Program] = [("Program charter", "docs/programs"), ("Roadmap", "roadmaps")],
        [SeedLinkKind.Project] = [("Project charter", "docs/projects"), ("Status report", "reports/projects")],
        [SeedLinkKind.StrategicInitiative] = [("Business case", "docs/initiatives"), ("Outcome dashboard", "dashboards")],
        [SeedLinkKind.Incident] = [("Runbook", "runbooks"), ("Postmortem", "postmortems")],
    };

    /// <summary>
    /// The cell for one record, or null when the record gets no links — roughly a third of each kind, so a seeded
    /// dataset shows records both with links and without.
    /// </summary>
    public static string? For(SeedLinkKind kind, string naturalKey)
    {
        var count = (int)(StableHash(naturalKey) % 3);
        if (count == 0)
            return null;

        var slug = Slug(naturalKey);
        return string.Join("; ", _catalog[kind].Take(count).Select(l => $"{l.Name}|https://example.com/{l.Path}/{slug}"));
    }

    /// <summary>FNV-1a, because <see cref="string.GetHashCode()"/> changes from one process to the next.</summary>
    private static uint StableHash(string value)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(value))
            hash = (hash ^ b) * 16777619u;

        return hash;
    }

    /// <summary>The key as a path segment: lower case, with every run of other characters a single hyphen.</summary>
    private static string Slug(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
                builder.Append(c);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }

        var slug = builder.ToString().TrimEnd('-');
        return slug.Length == 0 ? "record" : slug;
    }
}

/// <summary>The kinds of record a seeded import carries links on.</summary>
public enum SeedLinkKind
{
    Team,
    PlanningInterval,
    Objective,
    Risk,
    Portfolio,
    Program,
    Project,
    StrategicInitiative,
    Incident,
}
