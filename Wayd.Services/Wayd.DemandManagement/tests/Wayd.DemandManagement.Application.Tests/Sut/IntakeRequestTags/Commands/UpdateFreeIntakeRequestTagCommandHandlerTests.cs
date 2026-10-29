using FluentAssertions;
using Wayd.Common.Domain.Tagging;
using Wayd.DemandManagement.Application.IntakeRequestTags.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequestTags.Commands;

public sealed class UpdateFreeIntakeRequestTagCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private UpdateFreeIntakeRequestTagCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_RenamesTheFreeTag()
    {
        // Arrange
        var free = _scenario.FreeTag("follow up");
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new UpdateFreeIntakeRequestTagCommand(free.Id, "Follow up in Q3", "Revisit next quarter"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        free.Name.Should().Be("Follow up in Q3");
        free.Description.Should().Be("Revisit next quarter");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ToAnotherFreeTagsName_Fails()
    {
        // Arrange
        var free = _scenario.FreeTag("follow up");
        _scenario.FreeTag("Escalated");
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new UpdateFreeIntakeRequestTagCommand(free.Id, "escalated", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TagErrors.DuplicateFreeTagName);
        free.Name.Should().Be("follow up");
    }
}
