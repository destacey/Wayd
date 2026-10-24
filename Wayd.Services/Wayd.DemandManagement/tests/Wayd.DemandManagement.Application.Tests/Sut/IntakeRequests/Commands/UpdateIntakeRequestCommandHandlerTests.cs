using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class UpdateIntakeRequestCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private UpdateIntakeRequestCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    private static UpdateIntakeRequestCommand Retitled(IntakeRequest request, string title = "A new title") =>
        new(request.Id, title, request.Description, request.ExpectedBenefits, request.ChannelId, null);

    [Fact]
    public async Task Handle_ByTheRequesterWithoutUpdate_RecordsOnlyWhatChanged()
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
    public async Task Handle_ByAnUpdateHolderWhoCannotReadTheDetail_Fails()
    {
        // Arrange — the command carries the description, so nobody overwrites what they cannot read
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeRequestAuthorization.UpdatePermission);

        // Act
        var result = await CreateHandler().Handle(Retitled(request), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only someone who can read the request's detail can edit it.");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_BySomeoneWithoutUpdateWhoIsNotTheRequester_Fails()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee());

        // Act
        var result = await CreateHandler().Handle(Retitled(request), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only the requester or someone who can update requests may edit this request.");
    }

    [Fact]
    public async Task Handle_ForARequestOnAPrivateDeskTheCallerCannotSee_ReportsNotFound()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeRequestAuthorization.UpdatePermission);

        // Act
        var result = await CreateHandler().Handle(Retitled(request), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAccess.NotFoundError);
    }
}
