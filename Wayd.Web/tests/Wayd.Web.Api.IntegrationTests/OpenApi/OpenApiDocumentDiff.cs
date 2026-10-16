using System.Text;
using System.Text.Json.Nodes;

namespace Wayd.Web.Api.IntegrationTests.OpenApi;

/// <summary>One fact two OpenAPI documents state differently.</summary>
/// <param name="Location">Where the fact is, as <see cref="OpenApiFacts"/> keys it.</param>
/// <param name="Expected">The value in the document being matched, or <see cref="OpenApiFacts.Absent"/>.</param>
/// <param name="Actual">The value in the document under test, or <see cref="OpenApiFacts.Absent"/>.</param>
public sealed record OpenApiDifference(string Location, string Expected, string Actual)
{
    /// <summary>The last segment of the location: what kind of fact differs (<c>shape</c>, <c>required</c>, …).</summary>
    public string Kind => Location[(Location.LastIndexOf(" | ", StringComparison.Ordinal) + 3)..];

    /// <summary>The first segment of the location: the operation or schema the fact belongs to.</summary>
    public string Owner => Location[..Location.IndexOf(" | ", StringComparison.Ordinal)];
}

/// <summary>A kind of difference that is known, intended and explained, so a comparison does not report it.</summary>
/// <param name="Reason">Why the difference is acceptable, written for whoever reads the report.</param>
/// <param name="Matches">Whether a difference is of this kind.</param>
/// <param name="ReachesTools">
/// Whether the difference can change an MCP tool's input schema; false for what <c>OpenApiToolSchema</c>
/// normalizes or drops (formats it renames, examples, vendor extensions, empty descriptions).
/// </param>
public sealed record AcceptedDifference(string Reason, Func<OpenApiDifference, bool> Matches, bool ReachesTools = true);

/// <summary>Compares two OpenAPI documents by the facts <see cref="OpenApiFacts"/> reads from them.</summary>
public static class OpenApiDocumentDiff
{
    /// <summary>Every fact the documents state differently, in location order.</summary>
    public static IReadOnlyList<OpenApiDifference> Compare(JsonObject expected, JsonObject actual)
    {
        var expectedFacts = OpenApiFacts.Extract(expected);
        var actualFacts = OpenApiFacts.Extract(actual);

        return [.. expectedFacts.Keys.Union(actualFacts.Keys)
            .Order(StringComparer.Ordinal)
            .Select(key => new OpenApiDifference(
                key,
                expectedFacts.GetValueOrDefault(key, OpenApiFacts.Absent),
                actualFacts.GetValueOrDefault(key, OpenApiFacts.Absent)))
            .Where(d => d.Expected != d.Actual)];
    }

    /// <summary>The first accepted kind a difference is of, or null when none explains it.</summary>
    public static AcceptedDifference? ReasonFor(OpenApiDifference difference, IReadOnlyList<AcceptedDifference> accepted) =>
        accepted.FirstOrDefault(a => a.Matches(difference));

    /// <summary>
    /// A Markdown report: each accepted kind with its reason and count, then every difference no accepted kind
    /// explains, grouped by the kind of fact.
    /// </summary>
    public static string Report(
        IReadOnlyList<OpenApiDifference> differences,
        IReadOnlyList<AcceptedDifference> accepted,
        out IReadOnlyList<OpenApiDifference> unexplained)
    {
        var byRule = accepted.ToDictionary(a => a, _ => new List<OpenApiDifference>());
        var rest = new List<OpenApiDifference>();
        foreach (var difference in differences)
        {
            var rule = ReasonFor(difference, accepted);
            if (rule is null)
                rest.Add(difference);
            else
                byRule[rule].Add(difference);
        }

        unexplained = rest;

        var report = new StringBuilder();
        report.AppendLine("# OpenAPI document parity");
        report.AppendLine();
        report.AppendLine($"{differences.Count} differing facts: {differences.Count - rest.Count} accepted, {rest.Count} unexplained.");
        report.AppendLine();
        report.AppendLine("## Accepted");
        report.AppendLine();
        foreach (var (rule, matched) in byRule)
        {
            report.AppendLine($"- **{matched.Count}** — {rule.Reason}");
            foreach (var example in matched.Take(3))
                report.AppendLine($"  - `{example.Location}`: `{example.Expected}` → `{example.Actual}`");
        }

        report.AppendLine();
        report.AppendLine("## Unexplained");
        foreach (var group in rest.GroupBy(d => d.Kind).OrderByDescending(g => g.Count()))
        {
            report.AppendLine();
            report.AppendLine($"### {group.Key} ({group.Count()})");
            report.AppendLine();
            foreach (var difference in group)
                report.AppendLine($"- `{difference.Location}`: `{difference.Expected}` → `{difference.Actual}`");
        }

        return report.ToString();
    }
}
