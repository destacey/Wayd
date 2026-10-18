using FluentAssertions;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Web.Api.Models.Planning.StoryMaps;
using Wayd.Web.Api.Tests.Infrastructure;

namespace Wayd.Web.Api.Tests.Sut.Models.Planning.StoryMaps;

/// <summary>
/// The task persona request as the task persona patch endpoint builds it from the current task.
/// </summary>
public sealed class SetTaskPersonasRequestTests
{
    [Fact]
    public void FromDto_CopiesTheTasksPersonasIntoAListAPatchCanAppendTo()
    {
        // Arrange
        var task = new StoryMapTaskDto
        {
            Id = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            SwimLaneId = Guid.NewGuid(),
            Title = "Send the welcome pack",
            PersonaIds = [Guid.NewGuid(), Guid.NewGuid()],
            Checklist = [],
        };

        // Act
        var request = SetTaskPersonasRequest.FromDto(task);

        // Assert
        PatchRequestAssertions.ShouldSetEveryProperty(request);
        request.PersonaIds.Should().Equal(task.PersonaIds);
        request.PersonaIds.Should().NotBeSameAs(task.PersonaIds);
    }
}
