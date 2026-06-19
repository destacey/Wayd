using FluentAssertions;
using Wayd.ArchitectureTests.Helpers;

namespace Wayd.ArchitectureTests.Sut;

/// <summary>
/// Tests to enforce test project naming and organization conventions.
/// These tests ensure that test projects follow consistent patterns and properly
/// mirror the structure of the source code they test.
///
/// Test Project Conventions Enforced:
///
/// Naming:
/// - Test projects MUST end with ".Tests" or ".IntegrationTests"
/// - Test project names should mirror source project names with ".Tests" suffix
/// - Example: Wayd.Work.Domain -> Wayd.Work.Domain.Tests
/// - Example: Wayd.Work.Application -> Wayd.Work.Application.Tests
///
/// Project References:
/// - Test projects SHOULD reference their corresponding source project
/// - Example: Wayd.Work.Domain.Tests should reference Wayd.Work.Domain
///
/// Namespace Conventions:
/// - Test namespaces should mirror source namespaces with .Tests suffix
/// - Example: Wayd.Work.Domain.Models -> Wayd.Work.Domain.Tests.Sut.Models
///
/// Location:
/// - Test projects must be in a 'tests' folder (validated by FileStructureTests)
/// - Test projects should be at the same level as their source projects
/// </summary>
public class TestProjectTests
{
    private static readonly string SolutionRoot = AssemblyHelper.GetSolutionRoot();

    #region Test Project Naming Tests

    [Fact]
    public void TestProjects_ShouldEndWithTestsSuffix()
    {
        // Arrange
        var testProjectPaths = FileSystemHelper.GetAllTestProjectPaths();

        // Known exceptions: Shared test utilities projects
        var exceptions = new[] { "Wayd.Tests.Shared" };

        // Act
        var invalidTestProjects = testProjectPaths
            .Where(path =>
            {
                var projectName = Path.GetFileNameWithoutExtension(path);
                return !projectName.EndsWith(".Tests") &&
                       !projectName.EndsWith(".IntegrationTests") &&
                       !exceptions.Contains(projectName);
            })
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        // Assert
        invalidTestProjects.Should().BeEmpty(
            "All test projects should end with '.Tests' or '.IntegrationTests' (except shared test utilities like Wayd.Tests.Shared). Invalid projects: {0}",
            string.Join(", ", invalidTestProjects));
    }

    [Fact]
    public void ServiceDomainTests_ShouldMatchDomainProjectNames()
    {
        // Arrange
        var serviceTestProjects = FileSystemHelper.GetServiceTestProjects();
        var mismatches = new List<string>();

        // Act
        foreach (var testProject in serviceTestProjects.Where(p => p.Contains(".Domain.Tests")))
        {
            var testProjectName = Path.GetFileNameWithoutExtension(testProject);
            var expectedSourceProjectName = testProjectName.Replace(".Tests", "");

            // Check if corresponding source project exists
            var serviceFolder = Path.GetDirectoryName(Path.GetDirectoryName(testProject))!;
            var srcFolder = Path.Combine(serviceFolder, "src");

            if (Directory.Exists(srcFolder))
            {
                var sourceProjectPath = Path.Combine(srcFolder, expectedSourceProjectName, $"{expectedSourceProjectName}.csproj");
                if (!File.Exists(sourceProjectPath))
                {
                    mismatches.Add($"{testProjectName} (expected source: {expectedSourceProjectName})");
                }
            }
        }

        // Assert
        mismatches.Should().BeEmpty(
            "All Domain test projects should have a corresponding Domain source project with matching name. Mismatches: {0}",
            string.Join(", ", mismatches));
    }

    [Fact]
    public void ServiceApplicationTests_ShouldMatchApplicationProjectNames()
    {
        // Arrange
        var serviceTestProjects = FileSystemHelper.GetServiceTestProjects();
        var mismatches = new List<string>();

        // Act
        foreach (var testProject in serviceTestProjects.Where(p => p.Contains(".Application.Tests")))
        {
            var testProjectName = Path.GetFileNameWithoutExtension(testProject);
            var expectedSourceProjectName = testProjectName.Replace(".Tests", "");

            // Check if corresponding source project exists
            var serviceFolder = Path.GetDirectoryName(Path.GetDirectoryName(testProject))!;
            var srcFolder = Path.Combine(serviceFolder, "src");

            if (Directory.Exists(srcFolder))
            {
                var sourceProjectPath = Path.Combine(srcFolder, expectedSourceProjectName, $"{expectedSourceProjectName}.csproj");
                if (!File.Exists(sourceProjectPath))
                {
                    mismatches.Add($"{testProjectName} (expected source: {expectedSourceProjectName})");
                }
            }
        }

        // Assert
        mismatches.Should().BeEmpty(
            "All Application test projects should have a corresponding Application source project with matching name. Mismatches: {0}",
            string.Join(", ", mismatches));
    }

    #endregion

    #region Test Project References Tests

    [Fact]
    public void ServiceDomainTests_ShouldReferenceCorrespondingDomainProject()
    {
        // Arrange
        var serviceTestProjects = FileSystemHelper.GetServiceTestProjects();
        var missingReferences = new List<string>();

        // Act
        foreach (var testProjectPath in serviceTestProjects.Where(p => p.Contains(".Domain.Tests")))
        {
            var testProjectName = Path.GetFileNameWithoutExtension(testProjectPath);
            var expectedSourceProjectName = testProjectName.Replace(".Tests", "");

            // Read the test project file
            var projectContent = File.ReadAllText(testProjectPath);

            // Check if it references the corresponding source project
            if (!projectContent.Contains($"{expectedSourceProjectName}.csproj"))
            {
                missingReferences.Add($"{testProjectName} should reference {expectedSourceProjectName}");
            }
        }

        // Assert
        missingReferences.Should().BeEmpty(
            "All Domain test projects should reference their corresponding Domain project. Missing references: {0}",
            string.Join("; ", missingReferences));
    }

    [Fact]
    public void ServiceApplicationTests_ShouldReferenceCorrespondingApplicationProject()
    {
        // Arrange
        var serviceTestProjects = FileSystemHelper.GetServiceTestProjects();
        var missingReferences = new List<string>();

        // Act
        foreach (var testProjectPath in serviceTestProjects.Where(p => p.Contains(".Application.Tests") && !p.Contains("Common.Application.Tests")))
        {
            var testProjectName = Path.GetFileNameWithoutExtension(testProjectPath);
            var expectedSourceProjectName = testProjectName.Replace(".Tests", "");

            // Read the test project file
            var projectContent = File.ReadAllText(testProjectPath);

            // Check if it references the corresponding source project
            if (!projectContent.Contains($"{expectedSourceProjectName}.csproj"))
            {
                missingReferences.Add($"{testProjectName} should reference {expectedSourceProjectName}");
            }
        }

        // Assert
        missingReferences.Should().BeEmpty(
            "All Application test projects should reference their corresponding Application project. Missing references: {0}",
            string.Join("; ", missingReferences));
    }

    [Fact]
    public void IntegrationTests_ShouldReferenceCorrespondingSourceProject()
    {
        // Arrange
        var integrationTestProjects = FileSystemHelper.GetIntegrationTestProjects();
        var missingReferences = new List<string>();

        // Act
        foreach (var testProjectPath in integrationTestProjects)
        {
            var testProjectName = Path.GetFileNameWithoutExtension(testProjectPath);
            var expectedSourceProjectName = testProjectName
                .Replace(".IntegrationTests", "")
                .Replace(".Tests", "");

            // Read the test project file
            var projectContent = File.ReadAllText(testProjectPath);

            // Check if it references the corresponding source project
            if (!projectContent.Contains($"{expectedSourceProjectName}.csproj"))
            {
                missingReferences.Add($"{testProjectName} should reference {expectedSourceProjectName}");
            }
        }

        // Assert
        missingReferences.Should().BeEmpty(
            "All Integration test projects should reference their corresponding source project. Missing references: {0}",
            string.Join("; ", missingReferences));
    }

    #endregion

    #region Test Project Organization Tests

    [Fact]
    public void CommonTests_ShouldBeInCommonTestsFolder()
    {
        // Arrange
        var commonTestsFolder = Path.Combine(SolutionRoot, "Wayd.Common", "tests");

        if (!Directory.Exists(commonTestsFolder))
        {
            Assert.Fail("Wayd.Common/tests folder should exist");
            return;
        }

        // Act
        var testProjects = Directory.GetFiles(commonTestsFolder, "*.csproj", SearchOption.AllDirectories);

        // Assert
        testProjects.Should().NotBeEmpty("Wayd.Common/tests should contain test projects");

        foreach (var testProject in testProjects)
        {
            var projectName = Path.GetFileNameWithoutExtension(testProject);
            (projectName.EndsWith(".Tests") || projectName == "Wayd.Tests.Shared").Should().BeTrue(
                "Test project {0} should end with '.Tests' or be 'Wayd.Tests.Shared'", projectName);
        }
    }

    [Fact]
    public void ServiceTests_ShouldBeOrganizedByService()
    {
        // Arrange
        var servicesRoot = Path.Combine(SolutionRoot, "Wayd.Services");

        if (!Directory.Exists(servicesRoot))
        {
            return;
        }

        var violations = new List<string>();

        // Act - Check each service has proper test organization
        var serviceFolders = Directory.GetDirectories(servicesRoot)
            .Where(d => Path.GetFileName(d).StartsWith("Wayd."));

        foreach (var serviceFolder in serviceFolders)
        {
            var serviceName = Path.GetFileName(serviceFolder);
            var testsFolder = Path.Combine(serviceFolder, "tests");

            if (!Directory.Exists(testsFolder))
            {
                // It's okay if there are no tests yet
                continue;
            }

            var testProjects = Directory.GetFiles(testsFolder, "*.csproj", SearchOption.AllDirectories);

            foreach (var testProject in testProjects)
            {
                var testProjectName = Path.GetFileNameWithoutExtension(testProject);

                // Test project should start with the service name
                if (!testProjectName.StartsWith(serviceName))
                {
                    violations.Add($"{testProjectName} should start with {serviceName}");
                }
            }
        }

        // Assert
        violations.Should().BeEmpty(
            "All service test projects should be named consistently with their service. Violations: {0}",
            string.Join("; ", violations));
    }

    [Fact]
    public void TestProjects_ShouldUseXUnitTestFramework()
    {
        // Arrange
        var allTestProjects = FileSystemHelper.GetAllTestProjectPaths();
        var projectsMissingXUnit = new List<string>();

        // Known exceptions: Shared test utilities projects don't need test frameworks
        var exceptions = new[] { "Wayd.Tests.Shared" };

        // Act
        foreach (var testProjectPath in allTestProjects)
        {
            var projectContent = File.ReadAllText(testProjectPath);
            var projectName = Path.GetFileNameWithoutExtension(testProjectPath);

            // Skip exceptions
            if (exceptions.Contains(projectName))
            {
                continue;
            }

            // Check for xUnit
            if (!projectContent.Contains("PackageReference") || !projectContent.Contains("xunit"))
            {
                projectsMissingXUnit.Add(projectName);
            }
        }

        // Assert
        projectsMissingXUnit.Should().BeEmpty(
            "All test projects should use xUnit as the testing framework (except shared utilities). Projects missing xUnit: {0}",
            string.Join(", ", projectsMissingXUnit));
    }

    #endregion
}
