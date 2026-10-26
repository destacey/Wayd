using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class UpdateIntakeRequestCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private UpdateIntakeRequestCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    private static UpdateIntakeRequestCommand Retitled(IntakeRequest request, string title = "A new title") =>
        new(request.Id, title, request.Description, request.ExpectedBenefits, request.ChannelId, null);

    [Fact]
    public async Task Handle_ByTheRequesterOffTheDesk_RecordsOnlyWhatChanged()
    {
        // Arrange
        var requester = _scenario.Employee();
        var channel = _scenario.Channel();
        var request = _scenario.Request(_scenario.Desk(), requester);
        typeof(IntakeRequest).GetProperty(nameof(IntakeRequest.ChannelId))!.SetValue(request, channel.Id);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(Retitled(request), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Title.Should().Be("A new title");
        request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestRetitledEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ByAViewHolderWhoCannotReadTheDetail_Fails()
    {
        // Arrange — the command carries the description, so nobody overwrites what they cannot read
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(Retitled(request), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only someone who can read the request's detail can edit it.");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_BySomeoneOffTheDeskWhoIsNotTheRequester_Fails()
    {
        // Arrange — View reads an open desk's request in full, but editing it belongs to the desk
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(Retitled(request), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only the requester or an agent on the request's desk may edit this request.");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByAnAgentOnTheDeskHoldingNoClaim_Saves()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private, agentIds: [agent.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(Retitled(request), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Title.Should().Be(Retitled(request).Title);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_KeepingACommitmentWhoseMakerHasLeft_StillSaves()
    {
        // Arrange — the commitment stays a fact about who made it; an unrelated edit must not be locked by it
        var requester = _scenario.Employee();
        var leaver = _scenario.Employee(isActive: false);
        var channel = _scenario.Channel();
        var needBy = IntakeRequestNeedBy.Create(new LocalDate(2026, 12, 31), NeedByBasis.Commitment, "Steering committee",
            leaver.Id, new LocalDate(2026, 9, 3)).Value;
        var request = _scenario.Request(_scenario.Desk(), requester);
        typeof(IntakeRequest).GetProperty(nameof(IntakeRequest.ChannelId))!.SetValue(request, channel.Id);
        typeof(IntakeRequest).GetProperty(nameof(IntakeRequest.NeedBy))!.SetValue(request, needBy);
        _scenario.SignedInAs(requester);
        var command = new UpdateIntakeRequestCommand(request.Id, "Fixed a typo", request.Description, request.ExpectedBenefits,
            channel.Id, new IntakeRequestNeedByInput(needBy.Date, needBy.Basis, needBy.Reference, needBy.CommittedById, needBy.CommittedOn));

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Title.Should().Be("Fixed a typo");
    }

    [Fact]
    public async Task Handle_NamingANewCommitmentBySomeoneWhoHasLeft_Fails()
    {
        // Arrange
        var requester = _scenario.Employee();
        var channel = _scenario.Channel();
        var request = _scenario.Request(_scenario.Desk(), requester);
        typeof(IntakeRequest).GetProperty(nameof(IntakeRequest.ChannelId))!.SetValue(request, channel.Id);
        _scenario.SignedInAs(requester);
        var command = new UpdateIntakeRequestCommand(request.Id, request.Title, request.Description, request.ExpectedBenefits,
            channel.Id, new IntakeRequestNeedByInput(new LocalDate(2026, 12, 31), NeedByBasis.Commitment, "Steering committee",
                _scenario.Employee(isActive: false).Id, new LocalDate(2026, 9, 3)));

        // Act
        var result = await CreateHandler().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Whoever made the commitment must be an active employee.");
    }

    [Fact]
    public async Task Handle_ToAnArchivedChannel_FailsWithoutChangingAnything()
    {
        // Arrange
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(), requester);
        var archived = new IntakeChannelFaker().AsArchived().Generate();
        _scenario.DbContext.AddChannels(archived);
        var title = request.Title;
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(
            new UpdateIntakeRequestCommand(request.Id, "A new title", request.Description, request.ExpectedBenefits, archived.Id, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        request.Title.Should().Be(title);
        request.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ForARequestOnAPrivateDeskTheCallerCannotSee_ReportsNotFound()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(Retitled(request), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAccess.NotFoundError);
    }
}
