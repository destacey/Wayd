using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Queries;

public class GetProjectClassificationsQueryHandlerTests : IDisposable
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 9, 1, 0, 0);

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly GetProjectClassificationsQueryHandler _handler;
    private readonly ProjectPortfolio _portfolio;

    public GetProjectClassificationsQueryHandlerTests()
    {
        _handler = new GetProjectClassificationsQueryHandler(_dbContext);
        _portfolio = new ProjectPortfolioFaker().Generate();
        _dbContext.AddPortfolio(_portfolio);
    }

    private StrategicTheme NewTheme(string name)
    {
        var theme = new StrategicThemeFaker().WithName(name).Generate();
        _dbContext.AddPpmStrategicTheme(theme);
        return theme;
    }

    private Program NewProgram(params StrategicTheme[] themes)
    {
        var program = new ProgramFaker().WithPortfolioId(_portfolio.Id).Generate();
        if (themes.Length > 0)
            program.UpdateStrategicThemes(PpmActor.System, ProgramAncestryRoles.None, [.. themes.Select(t => t.Id)], Timestamp).IsSuccess.Should().BeTrue();
        _dbContext.AddProgram(program);
        return program;
    }

    private Project NewProject(Program? program = null, params StrategicTheme[] themes)
    {
        var faker = new ProjectFaker().WithPortfolioId(_portfolio.Id);
        if (program is not null)
            faker = faker.WithProgramId(program.Id);
        var project = faker.Generate();
        if (themes.Length > 0)
            project.UpdateStrategicThemes(PpmActor.System, ProjectAncestryRoles.None, [.. themes.Select(t => t.Id)], Timestamp).IsSuccess.Should().BeTrue();
        _dbContext.AddProject(project);
        return project;
    }

    [Fact]
    public async Task Handle_NoIds_ReturnsEmpty()
    {
        // Arrange
        var query = new GetProjectClassificationsQuery([]);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsPortfolioAndProgram()
    {
        // Arrange
        var program = NewProgram();
        var project = NewProject(program);

        // Act
        var result = await _handler.Handle(new GetProjectClassificationsQuery([project.Id]), TestContext.Current.CancellationToken);

        // Assert
        var classification = result.Should().ContainSingle().Subject;
        classification.ProjectKey.Should().Be(project.Key.Value);
        classification.Portfolio.Should().Be(new PpmRecordReference(_portfolio.Id, _portfolio.Key, _portfolio.Name));
        classification.Program.Should().Be(new PpmRecordReference(program.Id, program.Key, program.Name));
    }

    [Fact]
    public async Task Handle_ProjectWithoutProgram_HasNoProgram()
    {
        // Arrange
        var project = NewProject();

        // Act
        var result = await _handler.Handle(new GetProjectClassificationsQuery([project.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Program.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ProjectWithItsOwnThemes_UsesThemInPlaceOfTheProgramsThemes()
    {
        // Arrange
        var programTheme = NewTheme("Reduce cost to serve");
        var projectTheme = NewTheme("Grow self-serve revenue");
        var project = NewProject(NewProgram(programTheme), projectTheme);

        // Act
        var result = await _handler.Handle(new GetProjectClassificationsQuery([project.Id]), TestContext.Current.CancellationToken);

        // Assert
        var classification = result.Should().ContainSingle().Subject;
        classification.Themes.Select(t => t.Id).Should().BeEquivalentTo([projectTheme.Id]);
        classification.ThemesFromProgram.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ProjectWithoutThemes_InheritsTheProgramsThemes()
    {
        // Arrange
        var first = NewTheme("Trust & compliance");
        var second = NewTheme("Reduce cost to serve");
        var project = NewProject(NewProgram(first, second));

        // Act
        var result = await _handler.Handle(new GetProjectClassificationsQuery([project.Id]), TestContext.Current.CancellationToken);

        // Assert
        var classification = result.Should().ContainSingle().Subject;
        classification.Themes.Select(t => t.Name).Should().Equal("Reduce cost to serve", "Trust & compliance");
        classification.ThemesFromProgram.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoThemesAtEitherLevel_ReturnsNoThemes()
    {
        // Arrange
        var project = NewProject(NewProgram());

        // Act
        var result = await _handler.Handle(new GetProjectClassificationsQuery([project.Id]), TestContext.Current.CancellationToken);

        // Assert
        var classification = result.Should().ContainSingle().Subject;
        classification.Themes.Should().BeEmpty();
        classification.ThemesFromProgram.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UnknownIds_AreLeftOut()
    {
        // Arrange
        var project = NewProject();

        // Act
        var result = await _handler.Handle(new GetProjectClassificationsQuery([project.Id, Guid.NewGuid()]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.ProjectId.Should().Be(project.Id);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
