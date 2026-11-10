using FluentAssertions;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Incidents;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents;

public sealed class IncidentActivityTests
{
    [Fact]
    public void HighestByOwner_ShouldPreferTheLowestSortOrderThatIsStillActive()
    {
        // Arrange
        var productId = Guid.CreateVersion7();
        var closed = Guid.CreateVersion7();
        var severe = Guid.CreateVersion7();
        var milder = Guid.CreateVersion7();

        // Act
        var result = IncidentActivity.HighestByOwner(
        [
            new IncidentActivity.Row(productId, ProductStatusAlias.Closed, StatusCategory.Done, 0, closed, "SEV-1", "#E07A4C"),
            new IncidentActivity.Row(productId, ProductStatusAlias.Open, StatusCategory.Done, 0, Guid.CreateVersion7(), "Done", "#000000"),
            new IncidentActivity.Row(productId, ProductStatusAlias.Responding, StatusCategory.Active, 1, severe, "SEV-2", "#D4A017"),
            new IncidentActivity.Row(productId, ProductStatusAlias.Open, StatusCategory.Active, 2, milder, "SEV-3", "#7FA3BF"),
        ]);

        // Assert
        result.Should().ContainKey(productId);
        result[productId].Name.Should().Be("SEV-2");
        result[productId].Color.Should().Be("#D4A017");
    }

    [Fact]
    public void HighestByOwner_ShouldBreakASortOrderTieOnTheLevelId()
    {
        // Arrange
        var productId = Guid.CreateVersion7();
        var later = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var earlier = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // Act
        var result = IncidentActivity.HighestByOwner(
        [
            new IncidentActivity.Row(productId, ProductStatusAlias.Open, StatusCategory.Active, 0, later, "Later", "#111111"),
            new IncidentActivity.Row(productId, ProductStatusAlias.Open, StatusCategory.Active, 0, earlier, "Earlier", "#222222"),
        ]);

        // Assert
        result[productId].Name.Should().Be("Earlier");
        result[productId].Color.Should().Be("#222222");
    }

    [Fact]
    public void IsActive_ShouldFollowTheIncidentLifecycle()
    {
        // Assert
        IncidentActivity.IsActive(ProductStatusAlias.Open, StatusCategory.Active).Should().BeTrue();
        IncidentActivity.IsActive(ProductStatusAlias.Responding, StatusCategory.Proposed).Should().BeTrue();
        IncidentActivity.IsActive(ProductStatusAlias.Closed, StatusCategory.Done).Should().BeFalse();
        IncidentActivity.IsActive(ProductStatusAlias.Canceled, StatusCategory.Removed).Should().BeFalse();
        IncidentActivity.IsActive(ProductStatusAlias.Open, StatusCategory.Done).Should().BeFalse();
        IncidentActivity.IsActive(ProductStatusAlias.Open, StatusCategory.Removed).Should().BeFalse();
    }
}
