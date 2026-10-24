using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Events;

/// <summary>
/// Keeps every intake request event declaring its read tier, which is what a request's history is
/// filtered on. An event that declared none would be shown to every reader or to none, depending on how the
/// filter treated it — either is a failure nothing else would notice.
/// </summary>
public sealed class IntakeRequestEventTierTests
{
    public static TheoryData<string> IntakeRequestEventNames => [.. typeof(IDemandManagementEvent).Assembly
        .GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false }
            && typeof(DomainEvent).IsAssignableFrom(t)
            && t.Name.StartsWith("IntakeRequest"))
        .Select(t => t.Name)];

    [Theory]
    [MemberData(nameof(IntakeRequestEventNames))]
    public void EveryIntakeRequestEvent_DeclaresItsReadTier(string typeName)
    {
        // Arrange
        var type = typeof(IDemandManagementEvent).Assembly.GetTypes().Single(t => t.Name == typeName);

        // Act
        var declaresTier = typeof(IIntakeRequestEvent).IsAssignableFrom(type);

        // Assert
        declaresTier.Should().BeTrue($"{typeName} must implement IIntakeRequestEvent so the request's history can be filtered by tier");
    }
}
