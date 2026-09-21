using System.Text.RegularExpressions;
using FluentAssertions;
using Wayd.ArchitectureTests.Helpers;

namespace Wayd.ArchitectureTests.Sut;

/// <summary>
/// Keeps database transactions inside <c>BaseDbContext</c>, which is the only place that knows what a commit
/// still owes.
/// </summary>
/// <remarks>
/// <c>SaveChangesAsync</c> commits the rows together with the audit trail, activity log and outbox envelopes
/// that record them, and delivers what they raised only once that transaction commits. A save that joins a
/// transaction it did not open cannot know when the rows became durable, so it leaves its events queued —
/// and a caller who opens a transaction with <c>Database.BeginTransactionAsync</c> and commits it directly
/// never delivers them. The rows land and the events are silently gone, which is exactly the loss the
/// transaction was added to prevent.
/// <para>
/// So spanning several saves goes through <c>BeginUnitOfWork</c>, whose <c>CommitAsync</c> cannot leave the
/// delivery out. The preflight is the other sanctioned scope and never commits at all. Both live on the
/// context, which is why the rule is simply that nothing else opens a transaction.
/// </para>
/// </remarks>
public class TransactionScopeTests
{
    private const string BaseDbContextFile = "BaseDbContext.cs";

    /// <summary>
    /// Every <c>.cs</c> file in the shipped source: no build output, no test project, and nothing under a
    /// worktree. A test may open a transaction to set up or assert against one; only shipped code is bound
    /// by the rule.
    /// </summary>
    private static IEnumerable<string> ProductionSourceFiles()
    {
        var solutionRoot = AssemblyHelper.GetSolutionRoot();

        return Directory.GetFiles(solutionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !SegmentsOf(f).Any(s =>
                s is "bin" or "obj" or "tests" or ".claude" or "node_modules" or ".next"
                || s.EndsWith("Tests", StringComparison.Ordinal)
                || s.EndsWith("TestData", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Every <c>.cs</c> file, tests included. The retrying-strategy rule below binds a test fixture exactly
    /// as it binds shipped code: a fixture that switches retries on configures a context production cannot
    /// have, and every save through it fails.
    /// </summary>
    private static IEnumerable<string> AllSourceFiles()
    {
        var solutionRoot = AssemblyHelper.GetSolutionRoot();

        return Directory.GetFiles(solutionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !SegmentsOf(f).Any(s => s is "bin" or "obj" or ".claude" or "node_modules" or ".next"));
    }

    private static string[] SegmentsOf(string path) =>
        path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void OnlyTheContextOpensATransaction()
    {
        // Arrange
        // Matched on the call, not the bare name: the sync overload opens a transaction just as the async one
        // does, UseTransaction adopts someone else's, and a doc comment naming any of them is prose rather
        // than a call.
        var opensATransaction = new Regex(@"\.(BeginTransaction(Async)?|UseTransaction(Async)?)\s*\(", RegexOptions.Compiled);

        var offenders = ProductionSourceFiles()
            .Where(f => Path.GetFileName(f) != BaseDbContextFile)
            .Where(f => opensATransaction.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(AssemblyHelper.GetSolutionRoot(), f))
            .ToList();

        // Act & Assert — spanning several saves is BeginUnitOfWork's job; see this class's remarks for why
        offenders.Should().BeEmpty(
            "a transaction opened outside BaseDbContext commits without delivering the events the saves "
            + "inside it raised. Use dbContext.BeginUnitOfWork(cancellationToken) and commit through it.");
    }

    [Fact]
    public void NothingTurnsOnARetryingExecutionStrategy()
    {
        // Arrange
        // A retrying strategy refuses a transaction it did not start (ExecutionStrategyExistingTransaction),
        // and since the save owns one, turning this on fails every save rather than only the retried ones.
        // It reads as available — the seeders call CreateExecutionStrategy — so the ban is worth asserting.
        var enablesRetries = new Regex(@"\.EnableRetryOnFailure\s*\(", RegexOptions.Compiled);

        var offenders = AllSourceFiles()
            .Where(f => Path.GetFileName(f) != nameof(TransactionScopeTests) + ".cs")
            .Where(f => enablesRetries.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(AssemblyHelper.GetSolutionRoot(), f))
            .ToList();

        // Act & Assert
        offenders.Should().BeEmpty(
            "SaveChangesAsync opens the transaction that commits the rows with what records them, and a "
            + "retrying execution strategy refuses a transaction it did not start. A fixture that enables "
            + "retries fails every save in it. Wait the database out instead — SqlServerTestContainer does.");
    }

    [Fact]
    public void TheContextStillOffersAUnitOfWork()
    {
        // Arrange — the rule above is only safe while the sanctioned alternative exists
        var baseDbContext = ProductionSourceFiles().Single(f => Path.GetFileName(f) == BaseDbContextFile);

        // Act
        var source = File.ReadAllText(baseDbContext);

        // Assert
        source.Should().Contain("public async Task<UnitOfWork> BeginUnitOfWork(");
        source.Should().Contain("public async Task CommitAsync(");
    }
}
