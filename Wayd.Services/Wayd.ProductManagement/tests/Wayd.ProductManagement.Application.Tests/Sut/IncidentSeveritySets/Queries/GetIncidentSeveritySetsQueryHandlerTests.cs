using FluentAssertions;
using Wayd.ProductManagement.Application.IncidentSeveritySets.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.IncidentSeveritySets.Queries;

public sealed class GetIncidentSeveritySetsQueryHandlerTests : IncidentSeveritySetHandlerTestBase
{
    private GetIncidentSeveritySetsQueryHandler CreateSut() => new(DbContext);

    [Fact]
    public async Task Handle_ShouldReturnActiveSetsAndMarkTheSelection()
    {
        // Arrange
        var selected = SeedSystemSeveritySet("SEV");
        var inactive = SeedCustomSeveritySet("Retired");
        inactive.Deactivate(isSelected: false);
        SeedOrganizationSelection(selected);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetIncidentSeveritySetsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(1);
        result[0].IsSelected.Should().BeTrue();
        result[0].Name.Should().Be("SEV");
    }

    [Fact]
    public async Task Handle_ShouldIncludeInactiveSets_WhenRequested()
    {
        // Arrange
        var selected = SeedSystemSeveritySet();
        var inactive = SeedCustomSeveritySet("Retired");
        inactive.Deactivate(isSelected: false);
        SeedOrganizationSelection(selected);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetIncidentSeveritySetsQuery(IncludeInactive: true),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Single(d => d.Name == "Retired").IsActive.Should().BeFalse();
    }
}
