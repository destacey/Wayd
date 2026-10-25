using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Enums;

public sealed class IntakeDeskVisibilityExtensionsTests
{
    [Theory]
    [InlineData(IntakeDeskVisibility.Open, IntakeDeskVisibility.SummaryOnly, true)]
    [InlineData(IntakeDeskVisibility.Open, IntakeDeskVisibility.Private, true)]
    [InlineData(IntakeDeskVisibility.SummaryOnly, IntakeDeskVisibility.Private, true)]
    [InlineData(IntakeDeskVisibility.SummaryOnly, IntakeDeskVisibility.Open, false)]
    [InlineData(IntakeDeskVisibility.Private, IntakeDeskVisibility.Open, false)]
    [InlineData(IntakeDeskVisibility.Private, IntakeDeskVisibility.SummaryOnly, false)]
    [InlineData(IntakeDeskVisibility.Open, IntakeDeskVisibility.Open, false)]
    public void IsLessPrivateThan_OrdersOpenSummaryOnlyPrivate(IntakeDeskVisibility visibility, IntakeDeskVisibility other, bool expected)
    {
        // Act
        var result = visibility.IsLessPrivateThan(other);

        // Assert
        result.Should().Be(expected);
    }
}
