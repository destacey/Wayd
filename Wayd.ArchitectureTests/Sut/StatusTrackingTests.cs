using FluentAssertions;
using Wayd.ArchitectureTests.Helpers;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ArchitectureTests.Sut;

/// <summary>
/// Keeps the one status path without transitions deliberate: only the aggregates listed here take the status
/// their source reports, unchecked and with no <see cref="StatusTransition"/>.
/// </summary>
/// <remarks>
/// Deriving from <see cref="ManagedStatusEntity"/> instead of <see cref="StatusTrackedEntity"/> is a one-word
/// change that silently drops a record's transition checks and its history. Listing each one, with what keeps
/// its history instead, makes that a decision a reviewer sees.
/// </remarks>
public class StatusTrackingTests
{
    /// <summary>
    /// The aggregates whose status a source system decides, each with where its status history lives instead
    /// of <see cref="StatusTransition"/> rows.
    /// </summary>
    private static readonly Dictionary<Type, string> SourceStatusRecords = new();

    [Fact]
    public void ManagedStatusRecords_AreExactlyTheListedOnes()
    {
        // Arrange
        var domainTypes = AssemblyHelper.GetDomainAssemblies().SelectMany(a => a.GetTypes()).ToList();

        // Act
        var managed = ConcreteSubtypesOf<ManagedStatusEntity>(domainTypes);
        var tracked = ConcreteSubtypesOf<StatusTrackedEntity>(domainTypes);
        var unlisted = managed.Where(t => !SourceStatusRecords.ContainsKey(t)).Select(t => t.FullName);
        var stale = SourceStatusRecords.Keys.Where(t => !managed.Contains(t)).Select(t => t.FullName);

        // Assert
        // The tracked records prove the scan sees the domain's types, so an empty managed set is a finding
        // rather than a type-matching failure.
        tracked.Should().Contain(typeof(Product));
        string.Join(Environment.NewLine, unlisted).Should().BeEmpty(
            "a ManagedStatusEntity takes its source's status with no transition checks and no transition " +
            "rows. Add it to SourceStatusRecords with where its status history lives, or derive it from " +
            "StatusTrackedEntity if Wayd decides its moves");
        string.Join(Environment.NewLine, stale).Should().BeEmpty(
            "an entry in SourceStatusRecords must name a concrete ManagedStatusEntity");
    }

    private static List<Type> ConcreteSubtypesOf<T>(IEnumerable<Type> types) =>
        types.Where(t => t is { IsClass: true, IsAbstract: false } && typeof(T).IsAssignableFrom(t)).ToList();
}
