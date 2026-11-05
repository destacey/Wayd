using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class ChangeIntakeRequestStatusCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();
    private readonly Mock<IDiscussionWriter> _discussionWriter = new();
    private readonly List<PostDiscussionMessage> _posted = [];

    public ChangeIntakeRequestStatusCommandHandlerTests()
    {
        _discussionWriter
            .Setup(w => w.Post(It.IsAny<PostDiscussionMessage>(), It.IsAny<CancellationToken>()))
            .Callback((PostDiscussionMessage message, CancellationToken _) => _posted.Add(message))
            .ReturnsAsync(Guid.NewGuid());
    }

    private ChangeIntakeRequestStatusCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock, _scenario.Statuses.Object,
            _discussionWriter.Object);

    [Fact]
    public async Task Handle_WithAReply_PostsItWithTheMoveInOneSave()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Waiting on Requester"), null, null,
            new IntakeRequestReply("Budget", "Could you add the budget?")), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        request.StatusAlias.Should().Be(IntakeRequestStatusAlias.WaitingOnRequester);
        var posted = _posted.Should().ContainSingle().Subject;
        posted.ChannelType.Should().Be("demand.intake-request");
        posted.OwnerId.Should().Be(request.Id);
        posted.Kind.Should().Be(DiscussionMessageKind.Public);
        posted.Subject.Should().Be("Budget");
        posted.Body.Should().Be("Could you add the budget?");
        posted.RecordChange.Should().BeSameAs(request.DomainEvents.OfType<IntakeRequestStatusChangedEvent>().Single());
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithoutAReply_PostsNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("In Review"), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        _posted.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheReplyIsRefused_SavesNeitherTheMoveNorTheReply()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);
        _discussionWriter
            .Setup(w => w.Post(It.IsAny<PostDiscussionMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>(DiscussionErrors.ChannelLocked));

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Waiting on Requester"), null, null,
            new IntakeRequestReply(null, "Could you add the budget?")), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionErrors.ChannelLocked);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheReplysSaveCollides_FailsAsAConflict()
    {
        // Arrange — another first post opened the request's conversation in the same moment
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);
        _scenario.DbContext.FailNextSaveWith = new DbUpdateException("Duplicate conversation");

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Waiting on Requester"), null, null,
            new IntakeRequestReply(null, "Could you add the budget?")), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DiscussionAccessErrors.Conflict);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_WithAnEmptyReply_Fails(string body)
    {
        // Act
        var result = new ChangeIntakeRequestStatusCommandValidator().Validate(
            new ChangeIntakeRequestStatusCommand(Guid.NewGuid(), Guid.NewGuid(), null, null, new IntakeRequestReply(null, body)));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    private Guid StatusId(string name) => _scenario.Workflow.Status(name).Id;

    [Fact]
    public async Task Handle_ByAnAgentOnTheDeskWithNoClaim_MovesTheRequest()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("In Review"), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.StatusId.Should().Be(StatusId("In Review"));
        request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestStatusChangedEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByTheDemandManagementAdmin_MovesTheRequest()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Accepted"), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.StatusAlias.Should().Be(IntakeRequestStatusAlias.Accepted);
    }

    [Fact]
    public async Task Handle_ByTheRequester_FailsAndChangesNothing()
    {
        // Arrange — the requester reads the status; the desk decides it
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(), requester);
        _scenario.SignedInAs(requester, IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Accepted"), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
        request.StatusAlias.Should().Be(IntakeRequestStatusAlias.Submitted);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAPrivateRequestTheCallerCannotSee_ReportsNotFound()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Accepted"), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAccess.NotFoundError);
    }

    [Fact]
    public async Task Handle_ToAStatusOfAnotherWorkflow_Fails()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new ChangeIntakeRequestStatusCommand(request.Id, IntakeWorkflowFactory.Default("Elsewhere").Status("Accepted").Id, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"That status does not belong to '{_scenario.Workflow.Name}'.");
    }

    [Fact]
    public async Task Handle_ToDuplicate_OfARequestTheAgentCanSee_RecordsIt()
    {
        // Arrange — the surviving request is on another desk the agent does not work
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        var survivor = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), _scenario.Employee());
        _scenario.SignedInAs(agent, IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(
            new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Duplicate"), "Same as the export ask", survivor.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.DuplicateOfId.Should().Be(survivor.Id);
        request.StatusReason.Should().Be("Same as the export ask");
    }

    [Fact]
    public async Task Handle_ToDuplicate_OfAPrivateRequestTheAgentCannotSee_ReportsItAsNotFound()
    {
        // Arrange — the answer is the same as for a request that does not exist
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        var hidden = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(agent, IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(
            new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Duplicate"), "Same ask", hidden.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ChangeIntakeRequestStatusCommandHandler.SurvivorNotFoundError);
        request.DuplicateOfId.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ToDeclinedWithoutAReason_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Declined"), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Moving this request to 'Declined' needs a reason the requester can read.");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ToFulfilledWithAnOutcomeAndNoReason_MovesTheRequest()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.AcceptedRequestWithOutcomes(_scenario.Desk(agentIds: agent.Id), _scenario.Employee(), Guid.NewGuid());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Fulfilled"), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.StatusId.Should().Be(StatusId("Fulfilled"));
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ToFulfilledWithNoOutcomeAndNoReason_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.AcceptedRequestWithOutcomes(_scenario.Desk(agentIds: agent.Id), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new ChangeIntakeRequestStatusCommand(request.Id, StatusId("Fulfilled"), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Moving this request to 'Fulfilled' needs a reason the requester can read.");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
