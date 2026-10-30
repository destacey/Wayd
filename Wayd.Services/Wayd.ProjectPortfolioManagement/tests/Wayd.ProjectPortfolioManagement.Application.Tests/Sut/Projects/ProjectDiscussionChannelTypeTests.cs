using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects;

public sealed class ProjectDiscussionChannelTypeTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 29, 9, 0);
    private const string ViewPermission = "Permissions.Projects.View";
    private const string AdministerPermission = "Permissions.ProjectPortfolioManagement.Administer";

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _callerId = Guid.NewGuid();

    public ProjectDiscussionChannelTypeTests()
    {
        PpmDiscussionChannels.Register();
        Grant(ViewPermission, true);
        Grant(AdministerPermission, false);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_callerId);
    }

    private ProjectDiscussionChannelType CreateSut() => new(_dbContext, _currentPrincipal.Object);

    private void Grant(string permission, bool granted) =>
        _currentPrincipal
            .Setup(p => p.HasPermission(permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted);

    /// <summary>A project in a portfolio, and a program when given, with the parents' navigations loaded.</summary>
    private Project SeedProject(
        Dictionary<ProjectRole, HashSet<Guid>>? roles = null,
        Dictionary<ProjectPortfolioRole, HashSet<Guid>>? portfolioRoles = null,
        Dictionary<ProgramRole, HashSet<Guid>>? programRoles = null)
    {
        var portfolio = new ProjectPortfolioFaker().WithRoles(portfolioRoles).Generate();
        var faker = new ProjectFaker().WithPortfolio(portfolio).WithRoles(roles);
        if (programRoles is not null)
        {
            var program = new ProgramFaker().WithPortfolioId(portfolio.Id).WithRoles(programRoles).Generate();
            faker = faker.WithProgram(program);
        }

        var project = faker.Generate();
        _dbContext.AddProject(project);
        return project;
    }

    private Task<DiscussionAccess?> Resolve(Project project) =>
        CreateSut().Resolve(project.Id, TestContext.Current.CancellationToken);

    private static DiscussionAccess Access(bool canPost, bool canModerate) =>
        new(CanReadInternal: false, CanPost: canPost, CanPostInternal: false, CanModerate: canModerate);

    [Fact]
    public void ChannelType_ShouldBeTheProjectsFollowOwnerType()
    {
        // Act
        var channelType = CreateSut().ChannelType;

        // Assert
        // Persisted on every channel, and the key the conversation reads the project's activity under.
        channelType.Should().Be("ppm.project");
        channelType.Should().Be(PpmFollowOwners.Project.Key);
    }

    [Fact]
    public void Descriptor_ShouldSupportSubjectsButNoInternalNotesInTheProjectsSection()
    {
        // Act
        var descriptor = PpmDiscussionChannels.Project;

        // Assert
        descriptor.Section.Should().Be("Projects");
        descriptor.DisplayName.Should().Be("Project");
        descriptor.SupportsInternalNotes.Should().BeFalse();
        descriptor.SupportsSubjects.Should().BeTrue();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheCallerCannotViewProjects()
    {
        // Arrange
        var project = SeedProject(roles: new() { [ProjectRole.Owner] = [_callerId] });
        Grant(ViewPermission, false);

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheProjectDoesNotExist()
    {
        // Act
        var access = await CreateSut().Resolve(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheProjectDoesNotExist_ForACallerWithoutAnEmployee()
    {
        // Arrange
        SeedProject();
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var access = await CreateSut().Resolve(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ForAReaderWithNoRole_PostsButDoesNotModerate()
    {
        // Arrange
        var project = SeedProject(roles: new() { [ProjectRole.Owner] = [Guid.NewGuid()] });

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(Access(canPost: true, canModerate: false));
    }

    [Theory]
    [InlineData(ProjectRole.Owner, true)]
    [InlineData(ProjectRole.Manager, true)]
    [InlineData(ProjectRole.Sponsor, false)]
    [InlineData(ProjectRole.Member, false)]
    public async Task Resolve_OnTheProjectsOwnRoles_ModeratesOnlyAsADeliveryLeader(ProjectRole role, bool moderates)
    {
        // Arrange
        var project = SeedProject(roles: new() { [role] = [_callerId] });

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(Access(canPost: true, canModerate: moderates));
    }

    [Theory]
    [InlineData(ProjectPortfolioRole.Owner, true)]
    [InlineData(ProjectPortfolioRole.Manager, true)]
    [InlineData(ProjectPortfolioRole.Sponsor, false)]
    public async Task Resolve_OnThePortfoliosRoles_ModeratesOnlyAsADeliveryLeader(ProjectPortfolioRole role, bool moderates)
    {
        // Arrange
        var project = SeedProject(portfolioRoles: new() { [role] = [_callerId] });

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(Access(canPost: true, canModerate: moderates));
    }

    [Theory]
    [InlineData(ProgramRole.Owner, true)]
    [InlineData(ProgramRole.Manager, true)]
    [InlineData(ProgramRole.Sponsor, false)]
    public async Task Resolve_OnTheProgramsRoles_ModeratesOnlyAsADeliveryLeader(ProgramRole role, bool moderates)
    {
        // Arrange
        var project = SeedProject(programRoles: new() { [role] = [_callerId] });

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(Access(canPost: true, canModerate: moderates));
    }

    [Fact]
    public async Task Resolve_ForThePpmAdministrator_Moderates()
    {
        // Arrange
        var project = SeedProject();
        Grant(AdministerPermission, true);

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(Access(canPost: true, canModerate: true));
    }

    [Fact]
    public async Task Resolve_ForAReadOnlyToken_ReadsButNeitherPostsNorModerates()
    {
        // Arrange
        var project = SeedProject(roles: new() { [ProjectRole.Owner] = [_callerId] });
        Grant(AdministerPermission, true);
        _currentPrincipal.Setup(p => p.IsReadOnly).Returns(true);

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(Access(canPost: false, canModerate: false));
    }

    [Fact]
    public async Task Resolve_ForACallerWithoutAnEmployee_ReadsButDoesNotPost()
    {
        // Arrange
        var project = SeedProject();
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var access = await Resolve(project);

        // Assert
        access.Should().Be(Access(canPost: false, canModerate: false));
    }

    [Fact]
    public async Task MentionableAmong_InAMessage_KeepsActiveEmployeesOnly()
    {
        // Arrange
        var project = SeedProject();
        var active = new EmployeeFaker().AsActive().Generate();
        var inactive = new EmployeeFaker().AsInactive().Generate();
        _dbContext.AddEmployees([active, inactive]);

        // Act
        var mentionable = await CreateSut().MentionableAmong(project.Id, [active.Id, inactive.Id, Guid.NewGuid()],
            DiscussionMessageKind.Public, TestContext.Current.CancellationToken);

        // Assert
        mentionable.Should().BeEquivalentTo([active.Id]);
    }

    [Fact]
    public async Task MentionableAmong_InAnInternalNote_KeepsNobody()
    {
        // Arrange
        var project = SeedProject();
        var active = new EmployeeFaker().AsActive().Generate();
        _dbContext.AddEmployee(active);

        // Act
        var mentionable = await CreateSut().MentionableAmong(project.Id, [active.Id],
            DiscussionMessageKind.InternalNote, TestContext.Current.CancellationToken);

        // Assert
        mentionable.Should().BeEmpty();
    }

    [Fact]
    public async Task OnPostedAndOnEdited_ShouldChangeNothingOnTheProject()
    {
        // Arrange
        var project = SeedProject();
        var mentioned = Guid.NewGuid();
        var rolesBefore = project.Roles.ToList();
        var channel = DiscussionChannel.OpenForRecord(PpmDiscussionChannels.Project.Key, project.Id, EventActor.System, Now).Value;
        var message = DiscussionMessage.Post(channel, DiscussionMessageKind.Public, _callerId, "Vendor contract",
            "Can we sign this week?", [mentioned], EventActor.System, Now).Value;
        var sut = CreateSut();

        // Act
        await sut.OnPosted(channel, message, TestContext.Current.CancellationToken);
        await sut.OnEdited(channel, message, [mentioned], TestContext.Current.CancellationToken);

        // Assert
        project.Roles.Should().BeEquivalentTo(rolesBefore);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
