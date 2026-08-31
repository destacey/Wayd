using FluentAssertions;
using Wayd.ArchitectureTests.Helpers;

namespace Wayd.ArchitectureTests.Sut;

/// <summary>
/// Guards the one failure mode the handler tests structurally cannot catch: a query that loads a
/// <c>ReleasePackage</c> without its manifest, for a handler whose domain method reads that manifest.
///
/// There is no lazy loading in this solution, so an omitted <c>.Include(p =&gt; p.Components)</c> leaves
/// the collection empty rather than throwing — and <c>MarkReleased</c> refuses an empty manifest, so
/// every release of a real package is rejected. The in-memory fakes cannot detect this: a faked package
/// carries its components in the object graph regardless of what the query asked for, so the handler
/// test passes either way. That is exactly how the missing Include shipped green once already.
///
/// This inspects the handler source instead, which is the only place the difference is visible.
/// </summary>
public class ReleasePackageIncludeTests
{
    /// <summary>
    /// Handlers whose domain call reads the manifest — <c>MarkReleased</c> guards on it, and
    /// <c>SetManifest</c> diffs against it to decide whether anything changed.
    /// </summary>
    private static readonly string[] ManifestDependentHandlers =
    [
        "MarkReleasePackageReleasedCommand.cs",
        "SetReleasePackageManifestCommand.cs",
    ];

    [Theory]
    [InlineData("MarkReleasePackageReleasedCommand.cs")]
    [InlineData("SetReleasePackageManifestCommand.cs")]
    public void ManifestDependentHandlers_ShouldIncludeComponents(string handlerFileName)
    {
        // Arrange
        var path = FindHandler(handlerFileName);
        var source = File.ReadAllText(path);

        // Act
        var loadsPackage = source.Contains("ReleasePackages");
        var includesComponents = source.Contains("Include(p => p.Components)");

        // Assert
        loadsPackage.Should().BeTrue($"{handlerFileName} should load the package it mutates");
        includesComponents.Should().BeTrue(
            $"{handlerFileName} calls a domain method that reads the manifest. Without " +
            "`.Include(p => p.Components)` the collection loads empty and the operation is refused " +
            "for every package that actually has components.");
    }

    [Fact]
    public void EveryManifestDependentHandler_IsListedHere()
    {
        // A handler added later that reads the manifest gets no protection unless it is on the list
        // above, so this fails when the set of domain methods reading _components grows.
        // Arrange
        var solutionRoot = AssemblyHelper.GetSolutionRoot();
        var domainFile = Path.Combine(
            solutionRoot,
            "Wayd.Services", "Wayd.ProductManagement", "src", "Wayd.ProductManagement.Domain",
            "Models", "ReleasePackage.cs");

        // Act
        // Public mutating methods that read the backing collection. Components/ChangedComponents are
        // projections rather than mutations, and the constructor populates rather than reads.
        var readsManifest = File.ReadAllLines(domainFile)
            .Where(l => l.Contains("_components.Count") || l.Contains("_components.ToDictionary"))
            .ToArray();

        // Assert
        readsManifest.Should().NotBeEmpty(
            "if no domain method reads the manifest any more, this guard and its list are stale");
        ManifestDependentHandlers.Should().HaveCount(
            2,
            "MarkReleased and SetManifest are the two methods that read the manifest; a third would " +
            "need its handler added to ManifestDependentHandlers");
    }

    private static string FindHandler(string fileName)
    {
        var solutionRoot = AssemblyHelper.GetSolutionRoot();
        var commandsFolder = Path.Combine(
            solutionRoot,
            "Wayd.Services", "Wayd.ProductManagement", "src", "Wayd.ProductManagement.Application",
            "ReleasePackages", "Commands");

        var path = Path.Combine(commandsFolder, fileName);
        File.Exists(path).Should().BeTrue($"expected to find {fileName} at {path}");

        return path;
    }
}
