using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Seeding;
using Wayd.Tools.DataGeneration.Cli.Seeding.Areas;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// Splitting a set of rows into files an import will accept. An import rejects a file past the row cap it
/// publishes, so a seed large enough to pass that has to send more than one file — and the split has to
/// fall on a group boundary, because the rows within a group reference each other.
/// </summary>
public class SeedAreaTests
{
    /// <summary>The cap the atomic imports publish today.</summary>
    private const int ImportRowLimit = 10_000;

    private sealed record Row(string ProjectKey, int Number);

    /// <summary>Reaches the batching helper, which is protected because only an area needs it.</summary>
    private sealed class Harness() : SeedArea("test")
    {
        public override bool ShouldRun(SeedContext context) => false;

        public override Task Run(SeedContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public static IReadOnlyList<IReadOnlyList<Row>> Split(IEnumerable<Row> rows, int maxRowsPerFile = ImportRowLimit) =>
            Batch(rows, r => r.ProjectKey, maxRowsPerFile);
    }

    /// <summary>
    /// Groups of deliberately uneven size. Equal-sized groups make this fixture lie: with 25 rows each,
    /// a boundary drawn at a round row count lands exactly between two groups, so even a splitter that
    /// ignores groups entirely would look correct.
    /// </summary>
    private static IReadOnlyList<Row> Rows(int groups, int averagePerGroup) =>
        [.. Enumerable.Range(0, groups).SelectMany(g =>
            Enumerable.Range(0, averagePerGroup + (g % 7) - 3).Select(n => new Row($"P{g:D4}", n)))];

    [Fact]
    public void Batch_LeavesASmallSetInOneFile()
    {
        // Arrange & Act — the common case: nothing is gained by splitting a file the import accepts
        var rows = Rows(groups: 10, averagePerGroup: 20);
        var batches = Harness.Split(rows);

        // Assert
        batches.Should().ContainSingle();
        batches[0].Should().HaveCount(rows.Count);
    }

    [Fact]
    public void Batch_SplitsASetTooLargeForOneImport()
    {
        // Arrange & Act — 500 projects of 25 tasks is 12,500 rows, which is what a default seed produces
        // and what the import rejects
        var batches = Harness.Split(Rows(groups: 500, averagePerGroup: 25));

        // Assert
        batches.Count.Should().BeGreaterThan(1);
        batches.Should().AllSatisfy(b => b.Count.Should().BeLessThanOrEqualTo(ImportRowLimit));
    }

    [Fact]
    public void Batch_SplitsOnTheLimitItWasGiven()
    {
        // Arrange — a set far under the atomic cap, so only the limit passed in can cause a split
        var rows = Rows(groups: 40, averagePerGroup: 25);

        // Act
        var batches = Harness.Split(rows, maxRowsPerFile: 200);

        // Assert
        batches.Count.Should().BeGreaterThan(1);
        batches.Should().AllSatisfy(b => b.Count.Should().BeLessThanOrEqualTo(200));
    }

    [Fact]
    public void Batch_FillsAFileUpToTheLimitItself()
    {
        // Arrange — groups of exactly ten, so a file can land on the limit precisely; no margin is kept
        // under it, because a group is only added when it fits
        var rows = Enumerable.Range(0, 30)
            .SelectMany(g => Enumerable.Range(0, 10).Select(n => new Row($"P{g:D4}", n)))
            .ToList();

        // Act
        var batches = Harness.Split(rows, maxRowsPerFile: 100);

        // Assert
        batches.Select(b => b.Count).Should().Equal(100, 100, 100);
    }

    [Fact]
    public void Batch_KeepsEveryRowOfAGroupInTheSameFile()
    {
        // Arrange — this is the whole point: a child task names its parent by an ImportId in the same
        // file, so a project cut across two files hands the second one a child whose parent it never saw
        var batches = Harness.Split(Rows(groups: 500, averagePerGroup: 25));

        // Act
        var scattered = batches
            .SelectMany((b, i) => b.Select(r => (r.ProjectKey, Batch: i)))
            .GroupBy(x => x.ProjectKey)
            .Where(g => g.Select(x => x.Batch).Distinct().Count() > 1)
            .Select(g => g.Key)
            .ToList();

        // Assert
        scattered.Should().BeEmpty();
    }

    [Fact]
    public void Batch_LosesNoRows()
    {
        // Arrange
        var rows = Rows(groups: 500, averagePerGroup: 25);

        // Act
        var batched = Harness.Split(rows).SelectMany(b => b).ToList();

        // Assert — order is preserved too, so a file still reads parent-before-child
        batched.Should().Equal(rows);
    }

    [Fact]
    public void Batch_KeepsAGroupWholeEvenWhenItAloneExceedsTheLimit()
    {
        // Arrange & Act — one project with more tasks than an import accepts. Splitting it would break
        // the parent references it exists to hold, so it stays whole and the import rejects it: a named
        // failure beats a file that imports a broken tree.
        var batches = Harness.Split(Rows(groups: 1, averagePerGroup: ImportRowLimit + 500));

        // Assert
        batches.Should().ContainSingle();
        batches[0].Count.Should().BeGreaterThan(ImportRowLimit);
    }
}
