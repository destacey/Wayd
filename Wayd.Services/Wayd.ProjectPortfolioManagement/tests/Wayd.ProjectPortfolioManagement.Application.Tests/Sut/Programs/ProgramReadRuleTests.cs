using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.ProjectPortfolioManagement.Application.Programs;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Programs;

public sealed class ProgramReadRuleTests
{
    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly ProgramFaker _faker = new();

    private ProgramReadRule CreateSut() => new(_dbContext, _currentPrincipal.Object);

    private void GrantView(bool granted) =>
        _currentPrincipal
            .Setup(p => p.HasPermission("Permissions.Programs.View", It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted);

    [Fact]
    public void OwnerType_ShouldBeTheRegisteredProgramKey()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        // Persisted on every follow of a program, so a change here orphans them all.
        ownerType.Should().Be("ppm.program");
    }

    [Fact]
    public async Task ReadableIds_ShouldBeEveryProgram_WhenTheCallerCanViewPrograms()
    {
        // Arrange
        GrantView(true);
        var programs = _faker.Generate(3);
        _dbContext.AddPrograms(programs);

        // Act
        var readable = await CreateSut().ReadableIds(TestContext.Current.CancellationToken);

        // Assert
        readable.Should().NotBeNull();
        readable!.ToList().Should().BeEquivalentTo(programs.Select(p => p.Id));
    }

    [Fact]
    public async Task ReadableIds_ShouldBeNull_WhenTheCallerCannotViewPrograms()
    {
        // Arrange
        GrantView(false);
        _dbContext.AddPrograms(_faker.Generate(2));

        // Act
        var readable = await CreateSut().ReadableIds(TestContext.Current.CancellationToken);

        // Assert
        readable.Should().BeNull();
    }

    [Fact]
    public async Task Describe_ShouldNameEachRequestedProgram_AndOmitOnesThatDoNotExist()
    {
        // Arrange
        var mercury = new ProgramFaker().WithKey(42).WithName("Mercury").Generate();
        var vostok = new ProgramFaker().WithKey(43).WithName("Vostok").Generate();
        _dbContext.AddPrograms([mercury, vostok]);

        // Act
        var summaries = await CreateSut().Describe([mercury.Id, Guid.NewGuid()], TestContext.Current.CancellationToken);

        // Assert
        summaries.Should().ContainSingle();
        summaries[mercury.Id].Key.Should().Be("42");
        summaries[mercury.Id].Name.Should().Be("Mercury");
    }
}
