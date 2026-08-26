using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.Iterations.Dtos;
using Wayd.Planning.Application.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Web.Api.Controllers.Planning;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Tests.Sut.Controllers.Planning;

public sealed class PlanningIntervalsControllerBacklogTests
{
    private readonly AutoMocker _mocker = new();

    private PlanningIntervalsController CreateController()
    {
        var controller = _mocker.CreateInstance<PlanningIntervalsController>();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static SprintListDto Sprint(Guid id, string name) => new()
    {
        Id = id,
        Key = 1,
        Name = name,
        State = new SimpleNavigationDto { Id = 1, Name = "Active" },
        Team = new PlanningTeamNavigationDto
        {
            Id = Guid.NewGuid(),
            Key = 1,
            Name = "Core Services",
            Code = "CORE",
            Type = "Team"
        }
    };

    private static PlanningIntervalIterationSprintsDto Iteration(string name, params SprintListDto[] sprints) => new()
    {
        Id = Guid.NewGuid(),
        Key = 1,
        Name = name,
        Start = new LocalDate(2026, 1, 1),
        End = new LocalDate(2026, 1, 14),
        Category = new SimpleNavigationDto { Id = 1, Name = "Development" },
        Sprints = [.. sprints]
    };

    private void SetupIterations(params PlanningIntervalIterationSprintsDto[] iterations) =>
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetPlanningIntervalIterationSprintsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. iterations]);

    private void SetupBacklog(List<SprintBacklogItemDto> backlog) =>
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetSprintsBacklogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(backlog);

    [Fact]
    public async Task GetBacklog_AsksForEverySprintAcrossEveryIteration()
    {
        // Arrange — the per-iteration endpoint narrows to one iteration; this one
        // must not, or the PI backlog would only ever show a single iteration.
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        SetupIterations(
            Iteration("Iteration 1", Sprint(first, "S-1"), Sprint(second, "S-2")),
            Iteration("Iteration 2", Sprint(third, "S-3")));
        SetupBacklog([]);

        var controller = CreateController();

        // Act
        await controller.GetBacklog("7", TestContext.Current.CancellationToken);

        // Assert
        _mocker
            .GetMock<IDispatcher>()
            .Verify(
                d => d.Send(
                    It.Is<GetSprintsBacklogQuery>(q =>
                        q.SprintIds.Count() == 3 &&
                        q.SprintIds.Contains(first) &&
                        q.SprintIds.Contains(second) &&
                        q.SprintIds.Contains(third)),
                    It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public async Task GetBacklog_ForAPlanningIntervalWithNoMappedSprints_ReturnsAnEmptyBacklog()
    {
        // Arrange — a PI whose teams have not mapped their sprints yet.
        SetupIterations(Iteration("Iteration 1"));
        SetupBacklog([]);

        var controller = CreateController();

        // Act
        var result = await controller.GetBacklog("7", TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<List<SprintBacklogItemDto>>()
            .Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBacklog_ForAnUnknownPlanningInterval_ReturnsNotFound()
    {
        // Arrange — a well-formed key that matches no PI. A malformed one throws
        // in IdOrKey before reaching the handler, which every endpoint here shares.
        _mocker
            .GetMock<IDispatcher>()
            .Setup(d => d.Send(It.IsAny<GetPlanningIntervalIterationSprintsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<PlanningIntervalIterationSprintsDto>?)null);

        var controller = CreateController();

        // Act
        var result = await controller.GetBacklog("9999", TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
