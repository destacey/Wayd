using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class CreateIntakeRequestCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private CreateIntakeRequestCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock, _scenario.Statuses.Object);

    private static CreateIntakeRequestCommand Command(Guid deskId, Guid channelId, Guid? requesterId = null,
        IntakeRequestNeedByInput? needBy = null) =>
        new("Export invoices", "Finance needs a CSV export", "A day a month back", deskId, channelId, requesterId, needBy);

    [Fact]
    public async Task Handle_ForTheCaller_SubmitsToTheDeskWithTheCallerAsRequesterAndSubmitter()
    {
        // Arrange
        var caller = _scenario.Employee();
        _scenario.SignedInAs(caller);
        var desk = _scenario.Desk();
        var channel = _scenario.Channel();

        // Act
        var result = await CreateHandler().Handle(Command(desk.Id, channel.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = _scenario.DbContext.IntakeRequests.Single();
        created.RequesterId.Should().Be(caller.Id);
        created.SubmittedById.Should().Be(caller.Id);
        created.DeskId.Should().Be(desk.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_StartsTheRequestInSubmitted_FromTheWorkflowGoverningItsDesk()
    {
        // Arrange
        var caller = _scenario.Employee();
        _scenario.SignedInAs(caller);
        var desk = _scenario.Desk();

        // Act
        var result = await CreateHandler().Handle(Command(desk.Id, _scenario.Channel().Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = _scenario.DbContext.IntakeRequests.Single();
        created.StatusId.Should().Be(_scenario.SubmittedStatus.StatusId);
        created.StatusAlias.Should().Be(IntakeRequestStatusAlias.Submitted);
        // The desk is the scope, so a desk with a workflow of its own starts its requests there.
        _scenario.Statuses.Verify(r => r.ForAlias(DemandManagementWorkflowOwners.IntakeRequest.Key, desk.Id,
            (int)IntakeRequestStatusAlias.Submitted, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheDesksWorkflowCannotBeResolved_FailsAndSavesNothing()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        _scenario.Statuses.Setup(r => r.ForAlias(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<StatusRef>("No workflow is assigned for Intake Request. An administrator must assign one."));

        // Act
        var result = await CreateHandler().Handle(Command(_scenario.Desk().Id, _scenario.Channel().Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No workflow is assigned for Intake Request. An administrator must assign one.");
        _scenario.DbContext.IntakeRequests.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnSomeonesBehalf_RecordsBothPeople()
    {
        // Arrange
        var caller = _scenario.Employee();
        var requester = _scenario.Employee();
        _scenario.SignedInAs(caller);

        // Act
        var result = await CreateHandler().Handle(Command(_scenario.Desk().Id, _scenario.Channel().Id, requester.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = _scenario.DbContext.IntakeRequests.Single();
        created.RequesterId.Should().Be(requester.Id);
        created.SubmittedById.Should().Be(caller.Id);
    }

    [Fact]
    public async Task Handle_ToADeskRequestersCannotPick_Fails()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var desk = _scenario.Desk(isRequesterSelectable: false);

        // Act
        var result = await CreateHandler().Handle(Command(desk.Id, _scenario.Channel().Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Choose a desk that requesters can submit to.");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnInactiveRequester_Fails()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var requester = _scenario.Employee(isActive: false);

        // Act
        var result = await CreateHandler().Handle(Command(_scenario.Desk().Id, _scenario.Channel().Id, requester.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The requester must be an active employee.");
    }

    [Fact]
    public async Task Handle_ByACallerWhoseEmployeeIsInactive_Fails()
    {
        // Arrange — the submitter is named on the request too, so they must be active even on someone's behalf
        _scenario.SignedInAs(_scenario.Employee(isActive: false));
        var requester = _scenario.Employee();

        // Act
        var result = await CreateHandler().Handle(Command(_scenario.Desk().Id, _scenario.Channel().Id, requester.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only an active employee can submit a request.");
    }

    [Fact]
    public async Task Handle_WithACommitmentOnAnotherBasis_ReportsTheBasisRatherThanThePerson()
    {
        // Arrange — the shape of the need-by is checked before anyone it names is looked up
        _scenario.SignedInAs(_scenario.Employee());
        var needBy = new IntakeRequestNeedByInput(new LocalDate(2026, 12, 31), NeedByBasis.Regulatory, "SOX 404",
            Guid.NewGuid(), new LocalDate(2026, 9, 3));

        // Act
        var result = await CreateHandler().Handle(Command(_scenario.Desk().Id, _scenario.Channel().Id, needBy: needBy),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only a commitment records who made it and when.");
    }

    [Fact]
    public async Task Handle_WithACommitmentBySomeoneUnknown_Fails()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var needBy = new IntakeRequestNeedByInput(new LocalDate(2026, 12, 31), NeedByBasis.Commitment, "Steering committee",
            Guid.NewGuid(), new LocalDate(2026, 9, 3));

        // Act
        var result = await CreateHandler().Handle(Command(_scenario.Desk().Id, _scenario.Channel().Id, needBy: needBy),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Whoever made the commitment must be an active employee.");
    }

    [Fact]
    public async Task Handle_WithAValidNeedBy_StoresItWithTheRequest()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var needBy = new IntakeRequestNeedByInput(new LocalDate(2026, 12, 31), NeedByBasis.Regulatory, "PCI DSS 4.0", null, null);

        // Act
        var result = await CreateHandler().Handle(Command(_scenario.Desk().Id, _scenario.Channel().Id, needBy: needBy),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.DbContext.IntakeRequests.Single().NeedBy!.Reference.Should().Be("PCI DSS 4.0");
    }

    [Fact]
    public async Task Handle_WithAChannelTheDeskDoesNotOffer_FailsAndSavesNothing()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var desk = _scenario.Desk();
        var offered = _scenario.Channel();
        var other = _scenario.Channel();
        desk.ChangeChannels(false, [offered.Id], EventActor.System, _scenario.Clock.Now);

        // Act
        var result = await CreateHandler().Handle(Command(desk.Id, other.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.ChannelNotOfferedError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithoutAChannelToADeskOfferingNone_SubmitsWithNoChannel()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var desk = _scenario.Desk();
        _scenario.Channel();
        desk.ChangeChannels(false, [], EventActor.System, _scenario.Clock.Now);

        // Act
        var result = await CreateHandler().Handle(
            new CreateIntakeRequestCommand("Export invoices", "Finance needs a CSV export", null, desk.Id, null, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.DbContext.IntakeRequests.Single().ChannelId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithoutAChannelToADeskOfferingSome_Fails()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var desk = _scenario.Desk();
        _scenario.Channel();

        // Act
        var result = await CreateHandler().Handle(
            new CreateIntakeRequestCommand("Export invoices", "Finance needs a CSV export", null, desk.Id, null, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.ChannelRequiredError);
    }
}
