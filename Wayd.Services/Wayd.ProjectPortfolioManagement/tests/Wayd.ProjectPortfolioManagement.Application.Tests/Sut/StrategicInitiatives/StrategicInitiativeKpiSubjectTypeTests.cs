using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.StrategicInitiatives;

public sealed class StrategicInitiativeKpiSubjectTypeTests : IDisposable
{
    private const string ViewPermission = "Permissions.StrategicInitiatives.View";
    private const string UpdatePermission = "Permissions.StrategicInitiatives.Update";

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Instant.FromUtc(2026, 3, 10, 12, 0)));

    public StrategicInitiativeKpiSubjectTypeTests()
    {
        Grant(ViewPermission, true);
        Grant(UpdatePermission, true);
    }

    public void Dispose() => _dbContext.Dispose();

    private StrategicInitiativeKpiSubjectType CreateSut() => new(_dbContext, _currentPrincipal.Object);

    private void Grant(string permission, bool granted) =>
        _currentPrincipal
            .Setup(p => p.HoldsPermission(permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted);

    private StrategicInitiative SeedInitiative(StrategicInitiativeStatus status = StrategicInitiativeStatus.Active)
    {
        var initiative = new StrategicInitiativeFaker(_dateTimeProvider).WithStatus(status).Generate();
        _dbContext.AddStrategicInitiative(initiative);
        return initiative;
    }

    private Task<KpiSubjectAccess?> Resolve(StrategicInitiative initiative) =>
        CreateSut().Resolve(initiative.Id, TestContext.Current.CancellationToken);

    [Fact]
    public void SubjectType_ShouldBeTheStrategicInitiativeDescriptorsKey()
    {
        // Act
        var subjectType = CreateSut().SubjectType;

        // Assert
        subjectType.Should().Be("ppm.strategic-initiative");
        subjectType.Should().Be(PpmKpiSubjects.StrategicInitiative.Key);
        PpmKpiSubjects.StrategicInitiative.ActivityAggregateType.Should().Be("StrategicInitiative");
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheCallerCannotViewStrategicInitiatives()
    {
        // Arrange
        var initiative = SeedInitiative();
        Grant(ViewPermission, false);

        // Act
        var access = await Resolve(initiative);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheInitiativeDoesNotExist()
    {
        // Act
        var access = await CreateSut().Resolve(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resolve_ShouldLetManageExactlyWhenTheCallerHoldsTheUpdatePermission(bool granted)
    {
        // Arrange
        var initiative = SeedInitiative();
        Grant(UpdatePermission, granted);

        // Act
        var access = await Resolve(initiative);

        // Assert
        access.Should().Be(new KpiSubjectAccess(initiative.Name, initiative.Key.ToString(), granted));
    }

    [Theory]
    [InlineData(StrategicInitiativeStatus.Completed)]
    [InlineData(StrategicInitiativeStatus.Canceled)]
    public async Task Resolve_ShouldLetManage_WhenTheInitiativeIsClosed(StrategicInitiativeStatus status)
    {
        // Arrange
        var initiative = SeedInitiative(status);

        // Act
        var access = await Resolve(initiative);

        // Assert
        access!.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task Describe_ShouldLinkTheInitiativesThatExist()
    {
        // Arrange
        var initiative = SeedInitiative();

        // Act
        var links = await CreateSut().Describe([initiative.Id, Guid.NewGuid()], TestContext.Current.CancellationToken);

        // Assert
        links.Should().ContainSingle();
        links[initiative.Id].Should().Be(new ImportRecordLink(ImportRecordType.StrategicInitiative, initiative.Key.ToString(), initiative.Name));
    }
}
