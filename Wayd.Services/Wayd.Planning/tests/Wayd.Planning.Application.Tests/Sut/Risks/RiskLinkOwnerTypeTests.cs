using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Planning.Application.Risks;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain;
using Wayd.Planning.Domain.Tests.Data;
using Xunit;

namespace Wayd.Planning.Application.Tests.Sut.Risks;

public sealed class RiskLinkOwnerTypeTests : IDisposable
{
    private const string ViewPermission = "Permissions.Risks.View";
    private const string ManagePermission = "Permissions.Risks.Update";

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    public RiskLinkOwnerTypeTests()
    {
        _currentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    public void Dispose() => _dbContext.Dispose();

    private RiskLinkOwnerType CreateSut() => new(_dbContext, _currentPrincipal.Object);

    private Guid Seed()
    {
        var risk = new RiskFaker().Generate();
        _dbContext.AddRisk(risk);
        return risk.Id;
    }

    [Fact]
    public void OwnerType_ShouldBeFiledUnderTheRecordsActivityType()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        ownerType.Should().Be("planning.risk");
        PlanningLinkOwners.Risk.ActivityAggregateType.Should().Be("Risk");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resolve_ShouldLetAReaderManage_OnlyWithThePermissionThatEditsTheRecord(bool mayEdit)
    {
        // Arrange — every other permission is refused, so naming the wrong one fails here
        var id = Seed();
        _currentPrincipal.Setup(p => p.HasPermission(ManagePermission, It.IsAny<CancellationToken>())).ReturnsAsync(mayEdit);

        // Act
        var access = await CreateSut().Resolve(id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().Be(new LinkOwnerAccess(mayEdit));
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WithoutThePermissionThatReadsTheRecord()
    {
        // Arrange
        var id = Seed();
        _currentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _currentPrincipal.Setup(p => p.HasPermission(ManagePermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var access = await CreateSut().Resolve(id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheRecordDoesNotExist()
    {
        // Arrange
        Seed();

        // Act
        var access = await CreateSut().Resolve(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }
}
