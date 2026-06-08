using FluentAssertions;
using Mapster;
using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.Projects.Queries;

public class ProjectCustomProjectionMappingTests
{
    [Fact]
    public void ProjectListDto_CustomProjection_ShouldMapPhaseStatuses()
    {
        // Arrange
        var project = CreateProjectWithPhases();
        var now = SystemClock.Instance.GetCurrentInstant();
        var config = ProjectListDto.CreateTypeAdapterConfig(now, null);

        // Act
        var dto = new[] { project }
            .AsQueryable()
            .ProjectToType<ProjectListDto>(config)
            .Single();

        // Assert
        dto.Phases.Should().HaveCount(2);
        dto.Phases.Select(p => p.Order).Should().ContainInOrder(1, 2);
        dto.Phases[0].Status.Should().BeEquivalentTo(SimpleNavigationDto.FromEnum(TaskStatus.InProgress));
        dto.Phases[1].Status.Should().BeEquivalentTo(SimpleNavigationDto.FromEnum(TaskStatus.NotStarted));
    }

    [Fact]
    public void ProjectListDto_PositionOrdering_ShouldResetPerPortfolio_WhenResultsContainMultiplePortfolios()
    {
        // Arrange
        var portfolioA = new ProjectPortfolioFaker().WithData(name: "Shared Portfolio Name").Generate();
        var portfolioB = new ProjectPortfolioFaker().WithData(name: "Shared Portfolio Name").Generate();
        var expenditureCategory = new ExpenditureCategoryFaker().GenerateActive();

        var projectA2 = CreateRankedProject("Project A2", portfolioA, expenditureCategory, 20);
        var projectB2 = CreateRankedProject("Project B2", portfolioB, expenditureCategory, 15);
        var projectA1 = CreateRankedProject("Project A1", portfolioA, expenditureCategory, 10);
        var projectB1 = CreateRankedProject("Project B1", portfolioB, expenditureCategory, 5);

        var now = SystemClock.Instance.GetCurrentInstant();
        var config = ProjectListDto.CreateTypeAdapterConfig(now, null);

        // Act
        var ordered = new[] { projectA2, projectB2, projectA1, projectB1 }
            .AsQueryable()
            .ProjectToType<ProjectListDto>(config)
            .OrderBy(p => p.Portfolio.Id)
            .ThenBy(p => p.Rank)
            .ThenBy(p => p.Name)
            .ToList();

        Guid? currentPortfolioId = null;
        var position = 0;
        foreach (var project in ordered)
        {
            if (currentPortfolioId != project.Portfolio.Id)
            {
                currentPortfolioId = project.Portfolio.Id;
                position = 1;
            }

            project.Position = position++;
        }

        // Assert
        ordered.Where(p => p.Portfolio.Id == portfolioA.Id)
            .Select(p => (p.Name, p.Position))
            .Should().Equal(("Project A1", 1), ("Project A2", 2));

        ordered.Where(p => p.Portfolio.Id == portfolioB.Id)
            .Select(p => (p.Name, p.Position))
            .Should().Equal(("Project B1", 1), ("Project B2", 2));
    }

    [Fact]
    public void ProjectDetailsDto_CustomProjection_ShouldMapPhaseStatuses()
    {
        // Arrange
        var project = CreateProjectWithPhases();
        var now = SystemClock.Instance.GetCurrentInstant();
        var config = ProjectDetailsDto.CreateTypeAdapterConfig(now, employeeId: null);

        // Act
        var dto = new[] { project }
            .AsQueryable()
            .ProjectToType<ProjectDetailsDto>(config)
            .Single();

        // Assert
        dto.Phases.Should().HaveCount(2);
        dto.Phases.Select(p => p.Order).Should().ContainInOrder(1, 2);
        dto.Phases[0].Status.Should().BeEquivalentTo(SimpleNavigationDto.FromEnum(TaskStatus.InProgress));
        dto.Phases[1].Status.Should().BeEquivalentTo(SimpleNavigationDto.FromEnum(TaskStatus.NotStarted));
    }

    [Fact]
    public void ProjectDetailsDto_CustomProjection_ShouldMapStrategicInitiatives()
    {
        // Arrange
        var dateTimeProvider = new TestingDateTimeProvider(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var portfolio = new ProjectPortfolioFaker().Generate();
        var expenditureCategory = new ExpenditureCategoryFaker().GenerateActive();

        var project = new ProjectFaker()
            .WithData(
                portfolioId: portfolio.Id,
                expenditureCategoryId: expenditureCategory.Id)
            .Generate();

        typeof(Project).GetProperty(nameof(Project.Portfolio))!.SetValue(project, portfolio);
        typeof(Project).GetProperty(nameof(Project.ExpenditureCategory))!.SetValue(project, expenditureCategory);

        var initiativeA = new StrategicInitiativeFaker(dateTimeProvider)
            .WithData(name: "Initiative A", status: StrategicInitiativeStatus.Active, portfolioId: portfolio.Id)
            .Generate();
        var initiativeB = new StrategicInitiativeFaker(dateTimeProvider)
            .WithData(name: "Initiative B", status: StrategicInitiativeStatus.Proposed, portfolioId: portfolio.Id)
            .Generate();

        var linkA = (StrategicInitiativeProject)typeof(StrategicInitiativeProject)
            .GetMethod("Create", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [initiativeA.Id, project.Id])!;
        typeof(StrategicInitiativeProject).GetProperty(nameof(StrategicInitiativeProject.StrategicInitiative))!
            .SetValue(linkA, initiativeA);

        var linkB = (StrategicInitiativeProject)typeof(StrategicInitiativeProject)
            .GetMethod("Create", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [initiativeB.Id, project.Id])!;
        typeof(StrategicInitiativeProject).GetProperty(nameof(StrategicInitiativeProject.StrategicInitiative))!
            .SetValue(linkB, initiativeB);

        var links = Wayd.Tests.Shared.Extensions.GenericExtensions.GetPrivateHashSet<StrategicInitiativeProject>(project, "_strategicInitiativeProjects");
        links.Add(linkA);
        links.Add(linkB);

        var now = SystemClock.Instance.GetCurrentInstant();
        var config = ProjectDetailsDto.CreateTypeAdapterConfig(now, employeeId: null);

        // Act
        var dto = new[] { project }
            .AsQueryable()
            .ProjectToType<ProjectDetailsDto>(config)
            .Single();

        // Assert
        dto.StrategicInitiatives.Should().HaveCount(2);
        dto.StrategicInitiatives.Should().Contain(si => si.Id == initiativeA.Id && si.Key == initiativeA.Key && si.Name == "Initiative A");
        dto.StrategicInitiatives.Should().Contain(si => si.Id == initiativeB.Id && si.Key == initiativeB.Key && si.Name == "Initiative B");
    }

    [Fact]
    public void ProjectDetailsDto_CustomProjection_ShouldReturnEmptyWhenNoStrategicInitiatives()
    {
        // Arrange
        var project = CreateProjectWithPhases();
        var now = SystemClock.Instance.GetCurrentInstant();
        var config = ProjectDetailsDto.CreateTypeAdapterConfig(now, employeeId: null);

        // Act
        var dto = new[] { project }
            .AsQueryable()
            .ProjectToType<ProjectDetailsDto>(config)
            .Single();

        // Assert
        dto.StrategicInitiatives.Should().BeEmpty();
    }

    private static Project CreateProjectWithPhases()
    {
        var portfolio = new ProjectPortfolioFaker().Generate();
        var expenditureCategory = new ExpenditureCategoryFaker().GenerateActive();

        var project = new ProjectFaker()
            .WithData(
                portfolioId: portfolio.Id,
                expenditureCategoryId: expenditureCategory.Id)
            .Generate();

        typeof(Project).GetProperty(nameof(Project.Portfolio))!.SetValue(project, portfolio);
        typeof(Project).GetProperty(nameof(Project.ExpenditureCategory))!.SetValue(project, expenditureCategory);

        var firstPhase = new ProjectPhaseFaker()
            .WithData(
                projectId: project.Id,
                name: "Build",
                order: 2,
                status: TaskStatus.NotStarted)
            .Generate();

        var secondPhase = new ProjectPhaseFaker()
            .WithData(
                projectId: project.Id,
                name: "Design",
                order: 1,
                status: TaskStatus.InProgress)
            .Generate();

        project.AddToPrivateList("_phases", firstPhase);
        project.AddToPrivateList("_phases", secondPhase);

        return project;
    }

    private static Project CreateRankedProject(string name, ProjectPortfolio portfolio, ExpenditureCategory expenditureCategory, double rank)
    {
        var project = new ProjectFaker()
            .WithData(
                name: name,
                portfolioId: portfolio.Id,
                expenditureCategoryId: expenditureCategory.Id)
            .WithRank(rank)
            .Generate();

        typeof(Project).GetProperty(nameof(Project.Portfolio))!.SetValue(project, portfolio);
        typeof(Project).GetProperty(nameof(Project.ExpenditureCategory))!.SetValue(project, expenditureCategory);

        return project;
    }
}
