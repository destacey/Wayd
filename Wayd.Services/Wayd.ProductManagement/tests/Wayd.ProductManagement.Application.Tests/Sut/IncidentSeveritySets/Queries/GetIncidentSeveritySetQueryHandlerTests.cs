using FluentAssertions;
using Wayd.ProductManagement.Application.IncidentSeveritySets.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.IncidentSeveritySets.Queries;

public sealed class GetIncidentSeveritySetQueryHandlerTests : IncidentSeveritySetHandlerTestBase
{
    private GetIncidentSeveritySetQueryHandler CreateSut() => new(DbContext);

    [Fact]
    public async Task Handle_ShouldReturnTheSet()
    {
        // Arrange
        var set = SeedCustomSeveritySet("Ops");
        SeedOrganizationSelection(set);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetIncidentSeveritySetQuery(set.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Ops");
        result.IsSelected.Should().BeTrue();
        result.Levels.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WhenMissing()
    {
        // Arrange
        SeedOrganizationSelection(SeedSystemSeveritySet());
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetIncidentSeveritySetQuery(Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
