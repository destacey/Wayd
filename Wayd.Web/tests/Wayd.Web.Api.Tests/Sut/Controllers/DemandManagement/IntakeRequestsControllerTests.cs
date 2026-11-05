using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.Web.Api.Controllers.DemandManagement;
using Wayd.Web.Api.Models.DemandManagement.IntakeRequests;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Web.Api.Tests.Sut.Controllers.DemandManagement;

public sealed class IntakeRequestsControllerTests
{
    private readonly AutoMocker _mocker = new();

    private Mock<IDispatcher> Dispatcher => _mocker.GetMock<IDispatcher>();

    private IntakeRequestsController CreateController()
    {
        var controller = _mocker.CreateInstance<IntakeRequestsController>();
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact]
    public async Task GetIntakeRequest_FillsInEachWorkItemOutcomeFromWork_AndLeavesADeletedOneEmpty()
    {
        // Arrange
        var live = WorkItem("APP-42", "Export screen", "In Progress", StatusCategory.Active);
        var deletedId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        SetupRequest(
            Outcome(IntakeRequestOutcomeKind.WorkItem, live.Id),
            Outcome(IntakeRequestOutcomeKind.WorkItem, deletedId),
            Outcome(IntakeRequestOutcomeKind.NewProject, projectId));
        Dispatcher.Setup(d => d.Send(It.IsAny<GetWorkItemsByIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([live]);

        // Act
        var result = await CreateController().GetIntakeRequest("42", TestContext.Current.CancellationToken);

        // Assert
        var details = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<IntakeRequestDetailsDto>().Subject;
        details.Outcomes.Single(o => o.TargetId == live.Id).WorkItem.Should().BeEquivalentTo(new IntakeRequestOutcomeWorkItemDto
        {
            Id = live.Id,
            Key = "APP-42",
            Title = "Export screen",
            WorkspaceKey = "APP",
            Status = "In Progress",
            StatusCategory = StatusCategory.Active,
        });
        details.Outcomes.Single(o => o.TargetId == deletedId).WorkItem.Should().BeNull();
        details.Outcomes.Single(o => o.TargetId == projectId).WorkItem.Should().BeNull();
        Dispatcher.Verify(d => d.Send(
            It.Is<GetWorkItemsByIdsQuery>(q => q.Ids.Count == 2 && q.Ids.Contains(live.Id) && q.Ids.Contains(deletedId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetIntakeRequest_WithNoWorkItemOutcomes_DoesNotAskWork()
    {
        // Arrange
        SetupRequest(Outcome(IntakeRequestOutcomeKind.NewProject, Guid.NewGuid()));

        // Act
        var result = await CreateController().GetIntakeRequest("42", TestContext.Current.CancellationToken);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        Dispatcher.Verify(d => d.Send(It.IsAny<GetWorkItemsByIdsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddOutcome_ForAWorkItem_HandsInWhatWorkFound()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var workItem = WorkItem("APP-42", "Export screen", "Done", StatusCategory.Done);
        Dispatcher.Setup(d => d.Send(It.IsAny<GetWorkItemsByIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([workItem]);
        Dispatcher.Setup(d => d.Send(It.IsAny<AddIntakeRequestOutcomeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await CreateController().AddOutcome(requestId,
            new AddIntakeRequestOutcomeRequest { Kind = IntakeRequestOutcomeKind.WorkItem, WorkItemId = workItem.Id, Note = "Phase one" },
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        Dispatcher.Verify(d => d.Send(
            It.Is<AddIntakeRequestOutcomeCommand>(c => c.Id == requestId && c.Kind == IntakeRequestOutcomeKind.WorkItem
                && c.TargetId == workItem.Id && c.Note == "Phase one"
                && c.WorkItem != null && c.WorkItem.Id == workItem.Id && c.WorkItem.StatusCategory == StatusCategory.Done),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddOutcome_ForAWorkItemWorkDoesNotHave_HandsInNoWorkItem()
    {
        // Arrange
        var workItemId = Guid.NewGuid();
        Dispatcher.Setup(d => d.Send(It.IsAny<GetWorkItemsByIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Dispatcher.Setup(d => d.Send(It.IsAny<AddIntakeRequestOutcomeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(AddIntakeRequestOutcomeCommandHandler.WorkItemNotFoundError));

        // Act
        var result = await CreateController().AddOutcome(Guid.NewGuid(),
            new AddIntakeRequestOutcomeRequest { Kind = IntakeRequestOutcomeKind.WorkItem, WorkItemId = workItemId },
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        Dispatcher.Verify(d => d.Send(
            It.Is<AddIntakeRequestOutcomeCommand>(c => c.TargetId == workItemId && c.WorkItem == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddOutcome_ForAProject_DoesNotAskWork()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        Dispatcher.Setup(d => d.Send(It.IsAny<AddIntakeRequestOutcomeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await CreateController().AddOutcome(Guid.NewGuid(),
            new AddIntakeRequestOutcomeRequest { Kind = IntakeRequestOutcomeKind.NewProject, ProjectId = projectId },
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        Dispatcher.Verify(d => d.Send(
            It.Is<AddIntakeRequestOutcomeCommand>(c => c.TargetId == projectId && c.WorkItem == null),
            It.IsAny<CancellationToken>()), Times.Once);
        Dispatcher.Verify(d => d.Send(It.IsAny<GetWorkItemsByIdsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupRequest(params IntakeRequestOutcomeDto[] outcomes) =>
        Dispatcher.Setup(d => d.Send(It.IsAny<GetIntakeRequestQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntakeRequestDetailsDto { Summary = null!, Outcomes = outcomes });

    private static IntakeRequestOutcomeDto Outcome(IntakeRequestOutcomeKind kind, Guid targetId) => new()
    {
        Kind = kind,
        TargetId = targetId,
        AddedBy = new EmployeeNavigationDto { Id = Guid.NewGuid(), Key = 1, Name = "Morgan Agent" },
    };

    private static WorkItemDetailsNavigationDto WorkItem(string key, string title, string status, StatusCategory category) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Title = title,
        WorkspaceKey = "APP",
        Type = "Story",
        Status = status,
        StatusCategory = SimpleNavigationDto.FromEnum(category),
    };
}
