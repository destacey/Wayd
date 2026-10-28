using FluentAssertions;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequestTagCategories.Commands;

public sealed class DeleteIntakeRequestTagCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private DeleteIntakeRequestTagCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_WhenNoRequestCarriesIt_RemovesTheTag()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        var area = _scenario.TagCategory(allowsMany: true, "Payments", "Onboarding");
        var request = _scenario.Request(_scenario.Desk(), _scenario.Employee());
        _scenario.Tagged(request, area, "Onboarding");
        var payments = area.Tags.Single(t => t.Name == "Payments");

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestTagCommand(area.Id, payments.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        area.Tags.Select(t => t.Name).Should().Equal("Onboarding");
        area.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagRemovedEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenARequestCarriesIt_FailsAndKeepsIt()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        var request = _scenario.Request(_scenario.Desk(), _scenario.Employee());
        _scenario.Tagged(request, area, "Payments");

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestTagCommand(area.Id, area.Tags.Single().Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagCategory.TagInUseError);
        area.Tags.Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
