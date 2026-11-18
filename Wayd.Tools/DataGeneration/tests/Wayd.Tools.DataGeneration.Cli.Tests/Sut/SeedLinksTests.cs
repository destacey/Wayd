using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// The <c>Links</c> cell seeded records are imported with. The import rejects a row whose cell is malformed, so a bad
/// cell here would fail a whole seeding run.
/// </summary>
public class SeedLinksTests
{
    private static readonly string[] _keys = [.. Enumerable.Range(1, 60).Select(i => $"PROJ{i}")];

    [Fact]
    public void For_GivesTheSameRecordTheSameLinks()
    {
        // Arrange & Act
        var first = _keys.Select(k => SeedLinks.For(SeedLinkKind.Project, k)).ToList();
        var second = _keys.Select(k => SeedLinks.For(SeedLinkKind.Project, k)).ToList();

        // Assert
        second.Should().Equal(first);
    }

    [Fact]
    public void For_LeavesSomeRecordsWithoutLinks_AndGivesOthersOneOrTwo()
    {
        // Arrange & Act
        var counts = _keys
            .Select(k => SeedLinks.For(SeedLinkKind.Risk, k))
            .Select(cell => cell is null ? 0 : cell.Split(';').Length)
            .ToHashSet();

        // Assert
        counts.Should().BeEquivalentTo([0, 1, 2]);
    }

    [Theory]
    [InlineData(SeedLinkKind.Team, "PAY")]
    [InlineData(SeedLinkKind.PlanningInterval, "PI 2026.1")]
    [InlineData(SeedLinkKind.Objective, "PI 2026.1/PAY/3")]
    [InlineData(SeedLinkKind.Risk, "risk-17")]
    [InlineData(SeedLinkKind.Portfolio, "Consumer Banking")]
    [InlineData(SeedLinkKind.Program, "Payments; Modernization | Phase 2")]
    [InlineData(SeedLinkKind.Project, "APOLLO")]
    [InlineData(SeedLinkKind.StrategicInitiative, "Expand to the EU")]
    [InlineData(SeedLinkKind.Incident, "INC-0042")]
    public void For_WritesEntriesTheImportReads(SeedLinkKind kind, string key)
    {
        // Arrange — the same key with a suffix until one gets links, since a third of keys get none
        var cell = Enumerable.Range(0, 10).Select(i => SeedLinks.For(kind, $"{key}{i}")).First(c => c is not null);

        // Act
        var entries = cell!.Split(';', StringSplitOptions.TrimEntries);

        // Assert — Name|URL, split on the first |, with an absolute https address that holds neither separator
        entries.Should().AllSatisfy(entry =>
        {
            var separator = entry.IndexOf('|');
            separator.Should().BePositive();
            var url = entry[(separator + 1)..];
            url.Should().NotContain("|").And.NotContain(";").And.NotContain(" ");
            Uri.TryCreate(url, UriKind.Absolute, out var uri).Should().BeTrue();
            uri!.Scheme.Should().Be(Uri.UriSchemeHttps);
        });
    }
}
