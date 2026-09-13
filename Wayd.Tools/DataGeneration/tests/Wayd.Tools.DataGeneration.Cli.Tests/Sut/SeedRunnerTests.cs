using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Seeding;
using Wayd.Tools.DataGeneration.Cli.Seeding.Areas;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// The areas a seed actually registers, checked as a graph rather than as a run — the ordering is what a
/// new area is most likely to get wrong, and it can be verified without an environment to seed into.
/// </summary>
/// <remarks>
/// These assert the dependencies each area <em>declares</em>, not the order they happen to come out in.
/// Registration order already matches the required order today, so a position-based assertion passes even
/// with a dependency deleted — it would only start failing once someone reordered the list, which is the
/// moment the declaration is all that is holding the seed together.
/// </remarks>
public class SeedRunnerTests
{
    private static IReadOnlyList<string> Ordered() => [.. SeedAreaGraph.Order(SeedRunner.Areas).Select(a => a.Name)];

    private static ISeedArea Area(string name)
    {
        var area = SeedRunner.Areas.SingleOrDefault(a => a.Name == name);
        area.Should().NotBeNull($"'{name}' should be a registered seed area");

        return area!;
    }

    private static void ShouldDependOn(string area, params string[] dependencies) =>
        Area(area).DependsOn.Should().Contain(dependencies);

    [Fact]
    public void Areas_FormAnOrderableGraph()
    {
        // Arrange & Act
        var act = () => SeedAreaGraph.Order(SeedRunner.Areas);

        // Assert — every declared dependency exists, nothing cycles, no name is used twice
        act.Should().NotThrow();
    }

    [Fact]
    public void Areas_OrderEveryAreaAfterItsOwnDependencies()
    {
        // Arrange
        var ordered = Ordered().ToList();

        // Act
        var outOfOrder = SeedRunner.Areas
            .SelectMany(a => a.DependsOn.Select(d => (Area: a.Name, Dependency: d)))
            .Where(pair => ordered.IndexOf(pair.Dependency) > ordered.IndexOf(pair.Area))
            .ToList();

        // Assert
        outOfOrder.Should().BeEmpty();
    }

    [Fact]
    public void Areas_DeclareTheOrganizationBeforeAnythingThatStaffsFromIt()
    {
        // Arrange & Act & Assert — every PPM role column is an employee number, so the people have to
        // exist before a portfolio, program, project or initiative can name one
        ShouldDependOn(PpmArea.Portfolios, OrganizationArea.Employees);
        ShouldDependOn(PpmArea.Programs, OrganizationArea.Employees);
        ShouldDependOn(PpmArea.Projects, OrganizationArea.Employees);
        ShouldDependOn(PpmArea.Initiatives, OrganizationArea.Employees);
    }

    [Fact]
    public void Areas_DeclareEveryIdTheyReferenceAsADependency()
    {
        // Arrange & Act & Assert — these are the references that became ids, so each one is a stage that
        // cannot write its file until the run before it has reported what it created
        ShouldDependOn(PpmArea.Programs, PpmArea.Portfolios, PpmArea.Themes);
        ShouldDependOn(PpmArea.Projects, PpmArea.Portfolios, PpmArea.Programs, PpmArea.Themes, PpmArea.Settings);
        ShouldDependOn(PpmArea.Initiatives, PpmArea.Portfolios);
        ShouldDependOn(PpmArea.Finalize, PpmArea.Portfolios, PpmArea.Programs);
    }

    [Fact]
    public void Areas_FinalizeLast()
    {
        // Arrange & Act — a program or portfolio only closes once everything inside it is closed, so this
        // one is ordered by every area that creates something it might have to wait on
        ShouldDependOn(PpmArea.Finalize, PpmArea.Projects, PpmArea.Initiatives);

        // Assert
        Ordered().Last().Should().Be(PpmArea.Finalize);
    }

    [Fact]
    public void Areas_DeclareStageStatusesAfterTheTasksTheyWereComputedFrom()
    {
        // Arrange & Act & Assert — the generator derives each stage's status from its tasks, so setting it
        // before they land would state something the data does not yet support
        ShouldDependOn(PpmArea.ProjectStages, PpmArea.Projects, PpmArea.ProjectTasks);
    }

    [Fact]
    public void Areas_DeclareStaffingAfterThePeopleTheTeamsAndTheRoles()
    {
        // Arrange & Act & Assert — staffing resolves all three by natural key and creates none of them
        ShouldDependOn(
            OrganizationArea.Staffing,
            OrganizationArea.Employees,
            OrganizationArea.Teams,
            OrganizationArea.Roles);
    }

    [Fact]
    public void Areas_EnableProductManagementBeforePostingToIt()
    {
        // Arrange & Act & Assert — every Product Management endpoint answers 404 while its flag is off
        ShouldDependOn(ProductManagementArea.Environments, ProductManagementArea.FeatureFlag);
        ShouldDependOn(ProductManagementArea.Products, ProductManagementArea.FeatureFlag);
    }

    [Fact]
    public void Areas_DeclareEveryProductManagementIdTheyReference()
    {
        // Arrange & Act & Assert — a manifest line links to a version only if the version already exists,
        // and a release is accepted as released only once what it carries has been saved
        ShouldDependOn(ProductManagementArea.Versions, ProductManagementArea.Products);
        ShouldDependOn(ProductManagementArea.ReleasePackages, ProductManagementArea.Products, ProductManagementArea.Versions);
        ShouldDependOn(ProductManagementArea.Releases, ProductManagementArea.Products, ProductManagementArea.Versions, ProductManagementArea.ReleasePackages);
        ShouldDependOn(ProductManagementArea.Deployments, ProductManagementArea.Environments, ProductManagementArea.Versions, ProductManagementArea.ReleasePackages);
    }

    [Fact]
    public void Areas_DeclareEveryPlanningIdTheyReference()
    {
        // Arrange & Act & Assert — a roster, an objective and a risk each name a team by id, an objective
        // names its interval, and a risk names the people who reported and own it
        ShouldDependOn(PlanningArea.PlanningIntervals, OrganizationArea.Teams);
        ShouldDependOn(PlanningArea.Objectives, PlanningArea.PlanningIntervals, OrganizationArea.Teams);
        ShouldDependOn(PlanningArea.Risks, OrganizationArea.Teams, OrganizationArea.Employees);
    }

    [Fact]
    public void Areas_DeclarePlanningAfterStaffing()
    {
        // Arrange & Act & Assert — Planning resolves teams against its own copy, which fills asynchronously
        // after the teams import; staffing lands several runs later and gives it that time
        ShouldDependOn(PlanningArea.PlanningIntervals, OrganizationArea.Staffing);
        ShouldDependOn(PlanningArea.Risks, OrganizationArea.Staffing);
    }

    [Fact]
    public void Areas_DeclareTheHierarchyAfterTheTeamsItLinks()
    {
        // Arrange & Act & Assert
        ShouldDependOn(OrganizationArea.TeamHierarchy, OrganizationArea.Teams);
    }
}
