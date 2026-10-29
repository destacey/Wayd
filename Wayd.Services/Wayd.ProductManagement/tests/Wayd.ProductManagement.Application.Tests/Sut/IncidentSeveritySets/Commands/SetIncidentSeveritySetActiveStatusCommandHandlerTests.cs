using FluentAssertions;
using Wayd.ProductManagement.Application.IncidentSeveritySets.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.IncidentSeveritySets.Commands;

public sealed class SetIncidentSeveritySetActiveStatusCommandHandlerTests : IncidentSeveritySetHandlerTestBase
{
    private SetIncidentSeveritySetActiveStatusCommandHandler CreateSut() =>
        new(DbContext, Logger<SetIncidentSeveritySetActiveStatusCommandHandler>());

    [Fact]
    public async Task Handle_ShouldDeactivateACustomSet()
    {
        // Arrange
        var selected = SeedSystemSeveritySet();
        var custom = SeedCustomSeveritySet();
        SeedOrganizationSelection(selected);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetIncidentSeveritySetActiveStatusCommand(custom.Id, false),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        custom.IsActive.Should().BeFalse();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenDeactivatingTheSelectedSet()
    {
        // Arrange
        var set = SeedCustomSeveritySet();
        SeedOrganizationSelection(set);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SetIncidentSeveritySetActiveStatusCommand(set.Id, false),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The selected severity set cannot be deactivated.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
