using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class TagIntakeRequestCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private TagIntakeRequestCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ByAnAgentOnTheDesk_TagsTheRequest()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new TagIntakeRequestCommand(request.Id, area.Tags.Single().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Tags.Should().ContainSingle().Which.TagId.Should().Be(area.Tags.Single().Id);
        request.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagsChangedEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OnAnAxisTakingOne_ReplacesTheTagTheRequestHeld()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        var segment = _scenario.TagCategory(allowsMany: false, "Enterprise", "SMB");
        IntakeScenario.Tagged(request, segment, "Enterprise");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new TagIntakeRequestCommand(request.Id, segment.Tags.Single(t => t.Name == "SMB").Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Tags.Should().ContainSingle().Which.TagId.Should().Be(segment.Tags.Single(t => t.Name == "SMB").Id);
    }

    [Fact]
    public async Task Handle_ByTheRequester_FailsAndSavesNothing()
    {
        // Arrange — the request's own people see its tags but filing is the reviewers' call
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(), requester);
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new TagIntakeRequestCommand(request.Id, area.Tags.Single().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
        request.Tags.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByAViewHolderNotOnTheDesk_Fails()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(), _scenario.Employee());
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new TagIntakeRequestCommand(request.Id, area.Tags.Single().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
    }

    [Fact]
    public async Task Handle_ForARequestOnAPrivateDeskTheCallerCannotSee_ReportsNotFound()
    {
        // Arrange
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new TagIntakeRequestCommand(request.Id, area.Tags.Single().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAccess.NotFoundError);
    }

    [Fact]
    public async Task Handle_WithATagOnNoAxis_ReportsTheTagNotFound()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new TagIntakeRequestCommand(request.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagCategoryErrors.TagNotFoundError);
    }

    [Fact]
    public async Task Handle_WithAnInactiveTag_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        area.DeactivateTag(area.Tags.Single().Id, EventActor.System, _scenario.Clock.Now);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new TagIntakeRequestCommand(request.Id, area.Tags.Single().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.InactiveTagError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
