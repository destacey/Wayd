using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Xunit;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.StrategicInitiatives;

public sealed class StrategicInitiativeLinkOwnerTypeTests : IDisposable
{
    private const string ViewPermission = "Permissions.StrategicInitiatives.View";
    private const string ManagePermission = "Permissions.StrategicInitiatives.Update";

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    public StrategicInitiativeLinkOwnerTypeTests()
    {
        _currentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    public void Dispose() => _dbContext.Dispose();

    private StrategicInitiativeLinkOwnerType CreateSut() => new(_dbContext, _currentPrincipal.Object);

    private Guid Seed()
    {
        var initiative = new StrategicInitiativeFaker(new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 11, 17, 9, 0)))).Generate();
        _dbContext.AddStrategicInitiative(initiative);
        return initiative.Id;
    }

    [Fact]
    public void OwnerType_ShouldBeFiledUnderTheRecordsActivityType()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        ownerType.Should().Be("ppm.strategic-initiative");
        PpmLinkOwners.StrategicInitiative.ActivityAggregateType.Should().Be("StrategicInitiative");
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
