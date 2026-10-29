using FluentAssertions;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Tagging;
using Wayd.DemandManagement.Application.IntakeRequestTags;
using Wayd.DemandManagement.Application.IntakeRequestTags.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequestTags.Commands;

public sealed class MergeIntakeRequestTagCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private MergeIntakeRequestTagCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_MovesEveryRequestOnEveryDeskToTheTargetAndDeletesTheFreeTag()
    {
        // Arrange — a merge reaches desks whatever axes each offers
        var first = _scenario.Request(_scenario.AllowingFreeTags(_scenario.Desk()), _scenario.Employee());
        var second = _scenario.Request(_scenario.AllowingFreeTags(_scenario.Desk()), _scenario.Employee());
        var free = _scenario.FreeTag("payment");
        _scenario.TaggedFree(first, free);
        _scenario.TaggedFree(second, free);
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        var payments = area.Tags.Single();
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new MergeIntakeRequestTagCommand(free.Id, payments.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        first.Tags.Should().ContainSingle().Which.TagId.Should().Be(payments.Id);
        second.Tags.Should().ContainSingle().Which.TagId.Should().Be(payments.Id);
        _scenario.DbContext.IntakeRequestTags.Should().NotContain(free);
        free.DomainEvents.OfType<IntakeRequestFreeTagDeletedEvent>().Should().ContainSingle()
            .Which.MergedIntoTagId.Should().Be(payments.Id);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_FromATagOnAnAxis_Fails()
    {
        // Arrange
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        var free = _scenario.FreeTag();
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new MergeIntakeRequestTagCommand(area.Tags.Single().Id, free.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TagErrors.NotAFreeTag);
    }

    [Fact]
    public async Task Handle_IntoATagOnAnInactiveAxis_FailsAndKeepsTheFreeTag()
    {
        // Arrange
        var free = _scenario.FreeTag();
        var retired = new IntakeRequestTagCategoryFaker().WithIsActive(false).WithTags("Legacy").Generate();
        _scenario.DbContext.AddTagCategories(retired);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new MergeIntakeRequestTagCommand(free.Id, retired.Tags.Single().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagErrors.InactiveMergeTarget);
        _scenario.DbContext.IntakeRequestTags.Should().Contain(free);
    }
}
