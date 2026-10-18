using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson.Operations;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Queries;
using Wayd.Web.Api.Controllers.Ppm;
using Wayd.Web.Api.Models.Ppm.ProjectTasks;

namespace Wayd.Web.Api.Tests.Sut.Controllers.Ppm;

/// <summary>
/// The task update command is keyed on the task alone, so the patch endpoint is what holds a task to the
/// project its route names.
/// </summary>
public sealed class ProjectTasksControllerPatchTests
{
    private readonly AutoMocker _mocker = new();

    private ProjectTasksController CreateController()
    {
        var controller = _mocker.CreateInstance<ProjectTasksController>();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static ProjectTaskDto Task(Guid projectId) => new()
    {
        Id = Guid.NewGuid(),
        Key = "APOLLO-7",
        ProjectId = projectId,
        Name = "Draft the launch plan",
        Type = SimpleNavigationDto.Create(1, "Task"),
        Status = SimpleNavigationDto.Create(1, "Not Started"),
        Priority = SimpleNavigationDto.Create(2, "Medium"),
        Assignees = [],
        ProjectStageId = Guid.NewGuid(),
    };

    private static JsonPatchDocument<UpdateProjectTaskRequest> RenamePatch() =>
        new([new Operation<UpdateProjectTaskRequest>("replace", "/name", null, "Renamed")], System.Text.Json.JsonSerializerOptions.Web);

    [Fact]
    public async Task PatchProjectTask_AnswersNotFound_WhenTheTaskBelongsToAnotherProject()
    {
        // Arrange
        var task = Task(projectId: Guid.NewGuid());
        _mocker.GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetProjectTaskQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        var controller = CreateController();

        // Act
        var result = await controller.PatchProjectTask(Guid.NewGuid().ToString(), task.Id, RenamePatch(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        _mocker.GetMock<IDispatcher>().Verify(
            d => d.Send(It.IsAny<UpdateProjectTaskCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
