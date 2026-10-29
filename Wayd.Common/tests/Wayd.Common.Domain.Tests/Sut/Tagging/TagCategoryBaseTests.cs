using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tests.Data.Models;

namespace Wayd.Common.Domain.Tests.Sut.Tagging;

public sealed class TagCategoryBaseTests
{
    [Fact]
    public void AddTag_CollapsesRunsOfWhitespaceInTheName()
    {
        // Arrange — so a name typed while tagging matches it however its spaces were typed
        var sut = TestTagCategory.Create(allowsMany: true);

        // Act
        var result = sut.AddTag("  Data   Platform ", null, EventActor.System, Instant.MinValue);

        // Assert
        result.Value.Name.Should().Be("Data Platform");
    }

    [Fact]
    public void UpdateTagDetails_CollapsesRunsOfWhitespaceInTheName()
    {
        // Arrange
        var sut = TestTagCategory.Create(allowsMany: true, "ios");

        // Act
        var result = sut.UpdateTagDetails(sut["ios"].Id, "Apple   iOS", null, EventActor.System, Instant.MinValue);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut["Apple iOS"].Should().NotBeNull();
    }
}
