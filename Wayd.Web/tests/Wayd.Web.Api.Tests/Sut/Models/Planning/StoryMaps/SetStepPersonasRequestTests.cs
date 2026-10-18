using FluentAssertions;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Web.Api.Models.Planning.StoryMaps;
using Wayd.Web.Api.Tests.Infrastructure;

namespace Wayd.Web.Api.Tests.Sut.Models.Planning.StoryMaps;

/// <summary>
/// The step persona request as the step persona patch endpoint builds it from the current step.
/// </summary>
public sealed class SetStepPersonasRequestTests
{
    [Fact]
    public void FromDto_CopiesTheStepsPersonasIntoAListAPatchCanAppendTo()
    {
        // Arrange
        var step = new StoryMapStepDto
        {
            Id = Guid.NewGuid(),
            GoalId = Guid.NewGuid(),
            Name = "Sign the contract",
            PersonaIds = [Guid.NewGuid(), Guid.NewGuid()],
            Tasks = [],
        };

        // Act
        var request = SetStepPersonasRequest.FromDto(step);

        // Assert
        PatchRequestAssertions.ShouldSetEveryProperty(request);
        request.PersonaIds.Should().Equal(step.PersonaIds);
        request.PersonaIds.Should().NotBeSameAs(step.PersonaIds);
    }
}
