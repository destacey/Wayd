using FluentAssertions;
using Wayd.ProductManagement.Application.IncidentSeveritySets.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.IncidentSeveritySets.Commands;

public sealed class SelectIncidentSeveritySetCommandHandlerTests : IncidentSeveritySetHandlerTestBase
{
    private SelectIncidentSeveritySetCommandHandler CreateSut() =>
        new(DbContext, Logger<SelectIncidentSeveritySetCommandHandler>());

    [Fact]
    public async Task Handle_ShouldChangeTheOrganizationSelection()
    {
        // Arrange
        var current = SeedSystemSeveritySet("SEV");
        var other = SeedCustomSeveritySet("Alt");
        SeedOrganizationSelection(current);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SelectIncidentSeveritySetCommand(other.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(1);
        DbContext.IncidentSeveritySetAssignments.Should().ContainSingle().Which.SeveritySetId.Should().Be(other.Id);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSetIsInactive()
    {
        // Arrange
        var current = SeedSystemSeveritySet();
        var inactive = SeedCustomSeveritySet("Retired");
        inactive.Deactivate(isSelected: false);
        SeedOrganizationSelection(current);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new SelectIncidentSeveritySetCommand(inactive.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The selected severity set must be active.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
