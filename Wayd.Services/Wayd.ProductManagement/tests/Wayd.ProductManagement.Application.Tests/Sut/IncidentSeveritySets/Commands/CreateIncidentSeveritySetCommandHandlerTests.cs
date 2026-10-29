using FluentAssertions;
using Wayd.ProductManagement.Application.IncidentSeveritySets.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.IncidentSeveritySets.Commands;

public sealed class CreateIncidentSeveritySetCommandHandlerTests : IncidentSeveritySetHandlerTestBase
{
    private CreateIncidentSeveritySetCommandHandler CreateSut() =>
        new(DbContext, Logger<CreateIncidentSeveritySetCommandHandler>());

    [Fact]
    public async Task Handle_ShouldCreateTheSet()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CreateIncidentSeveritySetCommand("Ops", "On-call scale.", ["P0", "P1"]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(1);
        DbContext.IncidentSeveritySets.Should().ContainSingle().Which.Name.Should().Be("Ops");
    }

    [Fact]
    public async Task Handle_ShouldFail_OnDuplicateName()
    {
        // Arrange
        SeedCustomSeveritySet("Ops");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CreateIncidentSeveritySetCommand("Ops", null, ["A", "B"]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A severity set named 'Ops' already exists.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
