using FluentAssertions;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.ProjectTasks.Models;

public sealed class ProjectTaskIdOrKeyTests
{
    [Fact]
    public void Constructor_ReadsAGuidAsAnId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var idOrKey = new ProjectTaskIdOrKey(id.ToString());

        // Assert
        idOrKey.Value.Should().Be(id);
    }

    [Fact]
    public void Constructor_ReadsATaskKey()
    {
        // Act
        var idOrKey = new ProjectTaskIdOrKey("APOLLO-7");

        // Assert
        idOrKey.Value.Should().Be(new ProjectTaskKey("APOLLO-7"));
    }

    [Theory]
    [InlineData("APOLLO")]
    [InlineData("NO-SUCH-TASK")]
    public void Constructor_ThrowsNotFound_ForAValueThatCanNameNoTask(string value)
    {
        // Act
        var act = () => new ProjectTaskIdOrKey(value);

        // Assert
        act.Should().Throw<NotFoundException>();
    }
}
