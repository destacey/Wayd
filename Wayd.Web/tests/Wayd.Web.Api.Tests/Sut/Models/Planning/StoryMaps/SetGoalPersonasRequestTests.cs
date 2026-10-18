using FluentAssertions;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Web.Api.Models.Planning.StoryMaps;
using Wayd.Web.Api.Tests.Infrastructure;

namespace Wayd.Web.Api.Tests.Sut.Models.Planning.StoryMaps;

/// <summary>
/// The goal persona request as the goal persona patch endpoint builds it from the current goal.
/// </summary>
public sealed class SetGoalPersonasRequestTests
{
    [Fact]
    public void FromDto_CopiesTheGoalsPersonasIntoAListAPatchCanAppendTo()
    {
        // Arrange
        var goal = new StoryMapGoalDto
        {
            Id = Guid.NewGuid(),
            Name = "Onboard a customer",
            PersonaIds = [Guid.NewGuid(), Guid.NewGuid()],
            Steps = [],
        };

        // Act
        var request = SetGoalPersonasRequest.FromDto(goal);

        // Assert
        PatchRequestAssertions.ShouldSetEveryProperty(request);
        request.PersonaIds.Should().Equal(goal.PersonaIds);
        request.PersonaIds.Should().NotBeSameAs(goal.PersonaIds);
    }
}
