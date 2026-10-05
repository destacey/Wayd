using FluentAssertions;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Models.Organizations;
using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Common.Application.Tests.Sut.Models.Organizations;

public sealed class TeamIdOrCodeTests
{
    [Fact]
    public void Constructor_ReadsAGuidAsAnId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var idOrCode = new TeamIdOrCode(id.ToString());

        // Assert
        idOrCode.Value.Should().Be(id);
    }

    [Fact]
    public void Constructor_ReadsACodeInAnyCase()
    {
        // Act
        var idOrCode = new TeamIdOrCode("eng");

        // Assert
        idOrCode.Value.Should().Be(new TeamCode("ENG"));
    }

    [Theory]
    [InlineData("ENG-TEAM")]
    [InlineData("TOOLONGTEAMCODE")]
    public void Constructor_ThrowsNotFound_ForAValueThatCanNameNoTeam(string value)
    {
        // Act
        var act = () => new TeamIdOrCode(value);

        // Assert
        act.Should().Throw<NotFoundException>();
    }
}
