using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

public sealed class IncidentTests
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 4, 1, 12, 0, 0);

    [Fact]
    public void Create_ShouldOpenTheIncident()
    {
        // Arrange
        var commanderId = Guid.CreateVersion7();
        var severityLevelId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();

        // Act
        var result = Incident.Create(
            "Checkout unavailable",
            "Payments failing in production.",
            commanderId,
            severityLevelId,
            Timestamp,
            startedAt: null,
            recoveredAt: null,
            [productId],
            StatusRefFactory.Open(),
            EventActor.System,
            Timestamp);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Checkout unavailable");
        result.Value.CommanderEmployeeId.Should().Be(commanderId);
        result.Value.StatusAlias.Should().Be(ProductStatusAlias.Open);
        result.Value.ProductLinks.Should().ContainSingle().Which.ProductId.Should().Be(productId);
    }

    [Fact]
    public void Create_ShouldFail_WhenResponseStartsBeforeNotice()
    {
        // Act
        var result = Incident.Create(
            "Latency spike",
            null,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Timestamp,
            startedAt: Timestamp.Minus(Duration.FromMinutes(5)),
            recoveredAt: null,
            [],
            StatusRefFactory.Open(),
            EventActor.System,
            Timestamp);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
