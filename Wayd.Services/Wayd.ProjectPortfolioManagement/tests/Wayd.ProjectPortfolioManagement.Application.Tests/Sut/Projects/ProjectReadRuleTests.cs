using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects;

public sealed class ProjectReadRuleTests
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly ProjectFaker _faker = new();

    private ProjectReadRule CreateSut() => new(_dbContext, _currentPrincipal.Object);

    private void GrantView(bool granted) =>
        _currentPrincipal
            .Setup(p => p.HasPermission("Permissions.Projects.View", It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted);

    [Fact]
    public void OwnerType_ShouldBeTheRegisteredProjectKey()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        // Persisted on every follow of a project, so a change here orphans them all.
        ownerType.Should().Be("ppm.project");
    }

    [Fact]
    public async Task ReadableIds_ShouldBeEveryProject_WhenTheCallerCanViewProjects()
    {
        // Arrange
        GrantView(true);
        var projects = _faker.Generate(3);
        _dbContext.AddProjects(projects);

        // Act
        var readable = await CreateSut().ReadableIds(TestContext.Current.CancellationToken);

        // Assert
        readable.Should().NotBeNull();
        readable!.ToList().Should().BeEquivalentTo(projects.Select(p => p.Id));
    }

    [Fact]
    public async Task ReadableIds_ShouldBeNull_WhenTheCallerCannotViewProjects()
    {
        // Arrange
        GrantView(false);
        _dbContext.AddProjects(_faker.Generate(2));

        // Act
        var readable = await CreateSut().ReadableIds(TestContext.Current.CancellationToken);

        // Assert
        readable.Should().BeNull();
    }

    [Fact]
    public async Task Describe_ShouldNameEachRequestedProject_AndOmitOnesThatDoNotExist()
    {
        // Arrange
        var apollo = new ProjectFaker().WithKey(new ProjectKey("APOLLO")).WithName("Moon landing").Generate();
        var gemini = new ProjectFaker().WithKey(new ProjectKey("GEMINI")).WithName("Orbital docking").Generate();
        _dbContext.AddProjects([apollo, gemini]);
        var missing = Guid.NewGuid();

        // Act
        var summaries = await CreateSut().Describe([apollo.Id, missing], TestContext.Current.CancellationToken);

        // Assert
        summaries.Should().ContainSingle();
        summaries[apollo.Id].Key.Should().Be("APOLLO");
        summaries[apollo.Id].Name.Should().Be("Moon landing");
        summaries[apollo.Id].ParentKey.Should().BeNull();
    }
}
