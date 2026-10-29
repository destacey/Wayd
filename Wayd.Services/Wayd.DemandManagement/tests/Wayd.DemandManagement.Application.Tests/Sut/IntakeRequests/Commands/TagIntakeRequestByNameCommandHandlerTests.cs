using FluentAssertions;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.IntakeRequestTags;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class TagIntakeRequestByNameCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private TagIntakeRequestByNameCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_WhenAnAxisTheDeskOffersHasTheName_AppliesThatTag()
    {
        // Arrange — typing a curated tag's name by hand applies it, free tags off or on
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new TagIntakeRequestByNameCommand(request.Id, " payments "),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(area.Tags.Single().Id);
        request.Tags.Should().ContainSingle().Which.CategoryId.Should().Be(area.Id);
    }

    [Fact]
    public async Task Handle_WhenTwoAxesHaveTheName_Fails()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.AllowingFreeTags(_scenario.Desk(agentIds: [agent.Id])), _scenario.Employee());
        _scenario.TagCategory(allowsMany: true, "Payments");
        _scenario.TagCategory(allowsMany: true, "Payments");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new TagIntakeRequestByNameCommand(request.Id, "Payments"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagErrors.AmbiguousName);
        request.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ANewNameOnADeskNotAllowingFreeTags_FailsAndCreatesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new TagIntakeRequestByNameCommand(request.Id, "follow up"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.FreeTagsNotAllowedError);
        _scenario.DbContext.IntakeRequestTags.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ANewNameOnADeskAllowingFreeTags_CreatesAndAppliesAFreeTag()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.AllowingFreeTags(_scenario.Desk(agentIds: [agent.Id])), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new TagIntakeRequestByNameCommand(request.Id, " follow   up "),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = _scenario.DbContext.IntakeRequestTags.Should().ContainSingle().Subject;
        created.Id.Should().Be(result.Value);
        created.IsFree.Should().BeTrue();
        created.Name.Should().Be("follow up");
        request.Tags.Should().ContainSingle().Which.CategoryId.Should().BeNull();
        // The tag is saved before the request is tagged, so a name created by someone else meanwhile costs a
        // lookup rather than the caller's change.
        _scenario.DbContext.SaveChangesCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ANameAFreeTagHasInAnotherCase_ReusesIt()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.AllowingFreeTags(_scenario.Desk(agentIds: [agent.Id])), _scenario.Employee());
        var existing = _scenario.FreeTag("Follow Up");
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new TagIntakeRequestByNameCommand(request.Id, "follow up"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existing.Id);
        _scenario.DbContext.IntakeRequestTags.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ByTheRequester_Fails()
    {
        // Arrange — filing is the reviewers' call
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.AllowingFreeTags(_scenario.Desk()), requester);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new TagIntakeRequestByNameCommand(request.Id, "follow up"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
    }
}
