using FluentAssertions;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Models;
using Wayd.Work.Application.Workspaces.Models;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.Workspaces.Models;

public sealed class WorkspaceIdOrKeyTests
{
    [Fact]
    public void Constructor_ReadsAGuidAsAnId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var idOrKey = new WorkspaceIdOrKey(id.ToString());

        // Assert
        idOrKey.Value.Should().Be(id);
    }

    [Fact]
    public void Constructor_ReadsAWorkspaceKey()
    {
        // Act
        var idOrKey = new WorkspaceIdOrKey("ATLAS");

        // Assert
        idOrKey.Value.Should().Be(new WorkspaceKey("ATLAS"));
    }

    [Theory]
    [InlineData("NO-SUCH-WORKSPACE")]
    [InlineData("1ATLAS")]
    public void Constructor_ThrowsNotFound_ForAValueThatCanNameNoWorkspace(string value)
    {
        // Act
        var act = () => new WorkspaceIdOrKey(value);

        // Assert
        act.Should().Throw<NotFoundException>();
    }
}
