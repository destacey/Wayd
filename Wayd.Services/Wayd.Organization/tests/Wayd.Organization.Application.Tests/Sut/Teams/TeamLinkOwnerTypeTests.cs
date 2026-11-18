using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Organization.Application.Teams;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain;
using Wayd.Organization.Domain.Tests.Data;
using Xunit;

namespace Wayd.Organization.Application.Tests.Sut.Teams;

public sealed class TeamLinkOwnerTypeTests : IDisposable
{
    private const string ViewPermission = "Permissions.Teams.View";
    private const string ManagePermission = "Permissions.Teams.Update";

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    public TeamLinkOwnerTypeTests()
    {
        _currentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    public void Dispose() => _dbContext.Dispose();

    private TeamLinkOwnerType CreateSut() => new(_dbContext, _currentPrincipal.Object);

    private Guid Seed()
    {
        var team = new TeamFaker().Generate();
        _dbContext.AddTeam(team);
        return team.Id;
    }

    [Fact]
    public void OwnerType_ShouldBeFiledUnderTheRecordsActivityType()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        ownerType.Should().Be("organization.team");
        OrganizationLinkOwners.Team.ActivityAggregateType.Should().Be("Team");
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
