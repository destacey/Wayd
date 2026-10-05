using FluentAssertions;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Models;

public sealed class ProjectIdOrKeyTests
{
    [Fact]
    public void Constructor_ReadsAGuidAsAnId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var idOrKey = new ProjectIdOrKey(id.ToString());

        // Assert
        idOrKey.Value.Should().Be(id);
    }

    [Fact]
    public void Constructor_ReadsAKeyInAnyCase()
    {
        // Act
        var idOrKey = new ProjectIdOrKey("apollo");

        // Assert
        idOrKey.Value.Should().Be(new ProjectKey("APOLLO"));
    }

    [Theory]
    [InlineData("NO-SUCH-PROJECT-KEY")]
    [InlineData("A")]
    [InlineData(" ")]
    public void Constructor_ThrowsNotFound_ForAValueThatCanNameNoProject(string value)
    {
        // Act
        var act = () => new ProjectIdOrKey(value);

        // Assert
        act.Should().Throw<NotFoundException>();
    }
}
