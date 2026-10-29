using FluentAssertions;
using Wayd.ProductManagement.Application.IncidentSeveritySets.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.IncidentSeveritySets.Commands;

public sealed class UpdateIncidentSeveritySetCommandHandlerTests : IncidentSeveritySetHandlerTestBase
{
    private UpdateIncidentSeveritySetCommandHandler CreateSut() =>
        new(DbContext, LevelUsage.Object, Logger<UpdateIncidentSeveritySetCommandHandler>());

    [Fact]
    public async Task Handle_ShouldUpdateAndAddALevel()
    {
        // Arrange
        var set = SeedCustomSeveritySet();
        var sut = CreateSut();
        var command = new UpdateIncidentSeveritySetCommand(
            set.Id,
            "Renamed",
            "Updated",
            [
                new IncidentSeveritySetLevelPayload(set.Levels[0].Id, "High"),
                new IncidentSeveritySetLevelPayload(set.Levels[1].Id, "Low"),
                new IncidentSeveritySetLevelPayload(null, "Info"),
            ]);

        // Act
        var result = await sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(1);
        set.Name.Should().Be("Renamed");
        set.Levels.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_ShouldFail_OnSystemSet()
    {
        // Arrange
        var set = SeedSystemSeveritySet();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new UpdateIncidentSeveritySetCommand(
                set.Id,
                "Other",
                null,
                set.Levels.Select(l => new IncidentSeveritySetLevelPayload(l.Id, l.Name)).ToList()),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("System severity sets cannot be edited.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSetNotFound()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new UpdateIncidentSeveritySetCommand(
                Guid.CreateVersion7(),
                "Missing",
                null,
                [new IncidentSeveritySetLevelPayload(null, "A"), new IncidentSeveritySetLevelPayload(null, "B")]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Severity set not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
