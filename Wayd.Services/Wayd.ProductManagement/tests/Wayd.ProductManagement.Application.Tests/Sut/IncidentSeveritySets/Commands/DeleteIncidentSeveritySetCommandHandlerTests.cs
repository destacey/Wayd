using FluentAssertions;
using Wayd.ProductManagement.Application.IncidentSeveritySets.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.IncidentSeveritySets.Commands;

public sealed class DeleteIncidentSeveritySetCommandHandlerTests : IncidentSeveritySetHandlerTestBase
{
    private DeleteIncidentSeveritySetCommandHandler CreateSut() =>
        new(DbContext, LevelUsage.Object, Logger<DeleteIncidentSeveritySetCommandHandler>());

    [Fact]
    public async Task Handle_ShouldDeleteAnUnusedCustomSet()
    {
        // Arrange
        var selected = SeedSystemSeveritySet();
        var custom = SeedCustomSeveritySet("Custom");
        SeedOrganizationSelection(selected);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new DeleteIncidentSeveritySetCommand(custom.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.IncidentSeveritySets.Should().ContainSingle().Which.Id.Should().Be(selected.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSelected()
    {
        // Arrange
        var set = SeedCustomSeveritySet();
        SeedOrganizationSelection(set);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new DeleteIncidentSeveritySetCommand(set.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The selected severity set cannot be deleted.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_OnSystemSet()
    {
        // Arrange
        var set = SeedSystemSeveritySet();
        SeedOrganizationSelection(set);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new DeleteIncidentSeveritySetCommand(set.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("System severity sets cannot be deleted.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
