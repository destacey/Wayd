using FluentAssertions;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Tagging;
using Wayd.DemandManagement.Application.IntakeRequestTags.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequestTags.Commands;

public sealed class DeleteFreeIntakeRequestTagCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private DeleteFreeIntakeRequestTagCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_UntagsEveryCarrierAndDeletesTheTag()
    {
        // Arrange
        var request = _scenario.Request(_scenario.AllowingFreeTags(_scenario.Desk()), _scenario.Employee());
        var free = _scenario.FreeTag();
        _scenario.TaggedFree(request, free);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new DeleteFreeIntakeRequestTagCommand(free.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        request.Tags.Should().BeEmpty();
        request.DomainEvents.OfType<IntakeRequestTagsChangedEvent>().Should().ContainSingle();
        _scenario.DbContext.IntakeRequestTags.Should().NotContain(free);
        free.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestFreeTagDeletedEvent>();
    }

    [Fact]
    public async Task Handle_ATagOnAnAxis_Fails()
    {
        // Arrange
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new DeleteFreeIntakeRequestTagCommand(area.Tags.Single().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TagErrors.NotAFreeTag);
    }
}
