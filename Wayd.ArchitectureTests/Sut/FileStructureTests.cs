using FluentAssertions;
using Wayd.ArchitectureTests.Helpers;
using static Wayd.ArchitectureTests.Helpers.FileSystemHelper;

namespace Wayd.ArchitectureTests.Sut;

/// <summary>
/// Tests to enforce consistent file and folder structure conventions across the solution.
/// These tests ensure that projects are organized according to the modular monolith structure.
///
/// Expected Structure:
///
/// Wayd.Common/
///   src/
///     Wayd.Common/
///     Wayd.Common.Domain/
///     Wayd.Common.Application/
///   tests/
///     Wayd.Common.Tests/
///     Wayd.Common.Domain.Tests/
///     Wayd.Common.Application.Tests/
///     Wayd.Tests.Shared/
///
/// Wayd.Infrastructure/
///   src/
///     Wayd.Infrastructure/
///     Wayd.Infrastructure.Migrators.MSSQL/
///
/// Wayd.Integrations/
///   src/
///     Wayd.Integrations.{IntegrationName}/
///   tests/
///     Wayd.Integrations.{IntegrationName}.Tests/
///     Wayd.Integrations.{IntegrationName}.IntegrationTests/ (optional)
///
/// Wayd.Services/
///   Wayd.{ServiceName}/
///     src/
///       Wayd.{ServiceName}.Domain/
///       Wayd.{ServiceName}.Application/
///     tests/
///       Wayd.{ServiceName}.Domain.Tests/
///       Wayd.{ServiceName}.Application.Tests/
///
/// Wayd.Web/
///   src/
///     Wayd.Web.Api/
///     wayd.web.reactclient/
///
/// Wayd.ArchitectureTests/ (exception - no src/tests folders)
///
/// Key Rules:
/// - Source projects MUST be in a 'src' folder
/// - Test projects MUST be in a 'tests' folder
/// - Test projects MUST end with '.Tests' or '.IntegrationTests'
/// - Service projects follow the pattern: Wayd.Services/{ServiceName}/src/ and /tests/
/// - Integration projects follow the pattern: Wayd.Integrations/src/ and /tests/
/// - Wayd.ArchitectureTests is the only exception to the src/tests folder rule
/// </summary>
public class FileStructureTests
{
    private static readonly string SolutionRoot = AssemblyHelper.GetSolutionRoot();

    #region Common Layer Structure Tests

    [Fact]
    public void CommonLayer_SourceProjects_ShouldBeInSrcFolder()
    {
        // Arrange
        var expectedProjects = new[]
        {
            "Wayd.Common",
            "Wayd.Common.Domain",
            "Wayd.Common.Application"
        };

        // Act & Assert
        foreach (var projectName in expectedProjects)
        {
            var projectPath = Path.Combine(SolutionRoot, "Wayd.Common", "src", projectName);
            Directory.Exists(projectPath).Should().BeTrue(
                "Common source project {0} should be in Wayd.Common/src/ folder", projectName);

            var csprojPath = Path.Combine(projectPath, $"{projectName}.csproj");
            File.Exists(csprojPath).Should().BeTrue(
                "Project file {0} should exist at {1}", $"{projectName}.csproj", csprojPath);
        }
    }

    [Fact]
    public void CommonLayer_TestProjects_ShouldBeInTestsFolder()
    {
        // Arrange
        var expectedTestProjects = new[]
        {
            "Wayd.Common.Tests",
            "Wayd.Common.Domain.Tests",
            "Wayd.Common.Application.Tests",
            "Wayd.Tests.Shared"
        };

        // Act & Assert
        foreach (var projectName in expectedTestProjects)
        {
            var projectPath = Path.Combine(SolutionRoot, "Wayd.Common", "tests", projectName);
            Directory.Exists(projectPath).Should().BeTrue(
                "Common test project {0} should be in Wayd.Common/tests/ folder", projectName);

            var csprojPath = Path.Combine(projectPath, $"{projectName}.csproj");
            File.Exists(csprojPath).Should().BeTrue(
                "Project file {0} should exist at {1}", $"{projectName}.csproj", csprojPath);
        }
    }

    #endregion

    #region Infrastructure Layer Structure Tests

    [Fact]
    public void InfrastructureLayer_SourceProjects_ShouldBeInSrcFolder()
    {
        // Arrange
        var expectedProjects = new[]
        {
            "Wayd.Infrastructure",
            "Wayd.Infrastructure.Migrators.MSSQL"
        };

        // Act & Assert
        foreach (var projectName in expectedProjects)
        {
            var projectPath = Path.Combine(SolutionRoot, "Wayd.Infrastructure", "src", projectName);
            Directory.Exists(projectPath).Should().BeTrue(
                "Infrastructure source project {0} should be in Wayd.Infrastructure/src/ folder", projectName);

            var csprojPath = Path.Combine(projectPath, $"{projectName}.csproj");
            File.Exists(csprojPath).Should().BeTrue(
                "Project file {0} should exist at {1}", $"{projectName}.csproj", csprojPath);
        }
    }

    #endregion

    #region Integration Layer Structure Tests

    [Fact]
    public void IntegrationLayer_SourceProjects_ShouldBeInSrcFolder()
    {
        // Arrange
        var integrationsRoot = Path.Combine(SolutionRoot, "Wayd.Integrations");
        if (!Directory.Exists(integrationsRoot))
        {
            Assert.Fail("Wayd.Integrations directory should exist");
            return;
        }

        var srcFolder = Path.Combine(integrationsRoot, "src");
        if (!Directory.Exists(srcFolder))
        {
            Assert.Fail("Wayd.Integrations/src directory should exist");
            return;
        }

        // Act
        var integrationProjects = Directory.GetDirectories(srcFolder)
            .Where(d => Path.GetFileName(d).StartsWith("Wayd.Integrations."))
            .ToList();

        // Assert
        integrationProjects.Should().NotBeEmpty("There should be at least one integration project in Wayd.Integrations/src/");

        foreach (var projectPath in integrationProjects)
        {
            var projectName = Path.GetFileName(projectPath);
            var csprojPath = Path.Combine(projectPath, $"{projectName}.csproj");
            File.Exists(csprojPath).Should().BeTrue(
                "Integration project {0} should have a .csproj file", projectName);
        }
    }

    [Fact]
    public void IntegrationLayer_TestProjects_ShouldBeInTestsFolder()
    {
        // Arrange
        var integrationsRoot = Path.Combine(SolutionRoot, "Wayd.Integrations");
        var testsFolder = Path.Combine(integrationsRoot, "tests");

        if (!Directory.Exists(testsFolder))
        {
            // It's okay if there are no tests yet
            return;
        }

        // Act
        var testProjects = Directory.GetDirectories(testsFolder)
            .Where(d =>
            {
                var name = Path.GetFileName(d);
                return name.StartsWith("Wayd.Integrations.") &&
                       (name.EndsWith(".Tests") || name.EndsWith(".IntegrationTests"));
            })
            .ToList();

        // Assert
        foreach (var projectPath in testProjects)
        {
            var projectName = Path.GetFileName(projectPath);
            var csprojPath = Path.Combine(projectPath, $"{projectName}.csproj");
            File.Exists(csprojPath).Should().BeTrue(
                "Integration test project {0} should have a .csproj file", projectName);
        }
    }

    [Fact]
    public void IntegrationLayer_TestProjects_ShouldHaveCorrectSuffix()
    {
        // Arrange
        var integrationsRoot = Path.Combine(SolutionRoot, "Wayd.Integrations");
        var testsFolder = Path.Combine(integrationsRoot, "tests");

        if (!Directory.Exists(testsFolder))
        {
            // It's okay if there are no tests yet
            return;
        }

        // Act
        var allTestFolders = Directory.GetDirectories(testsFolder)
            .Where(d => Path.GetFileName(d).StartsWith("Wayd.Integrations."))
            .ToList();

        var invalidTestProjects = allTestFolders
            .Where(d =>
            {
                var name = Path.GetFileName(d);
                return !name.EndsWith(".Tests") && !name.EndsWith(".IntegrationTests");
            })
            .Select(Path.GetFileName)
            .ToList();

        // Assert
        invalidTestProjects.Should().BeEmpty(
            "All integration test projects should end with '.Tests' or '.IntegrationTests'. Invalid: {0}",
            string.Join(", ", invalidTestProjects));
    }

    #endregion

    #region Service Layer Structure Tests

    [Fact]
    public void ServiceLayer_AllServices_ShouldHaveSrcFolder()
    {
        // Arrange - only check services that are in the solution file
        var serviceDirectories = GetSolutionServiceDirectories();

        // Assert
        serviceDirectories.Should().NotBeEmpty("There should be at least one service in the solution");

        var servicesWithoutSrc = serviceDirectories
            .Where(d => !Directory.Exists(Path.Combine(d, "src")))
            .Select(Path.GetFileName)
            .ToList();

        servicesWithoutSrc.Should().BeEmpty(
            "All services should have a 'src' folder. Missing: {0}",
            string.Join(", ", servicesWithoutSrc));
    }

    [Fact]
    public void ServiceLayer_SourceProjects_ShouldBeInSrcFolder()
    {
        // Arrange - only check services that are in the solution file
        var serviceDirectories = GetSolutionServiceDirectories();

        // Act & Assert
        foreach (var serviceDir in serviceDirectories)
        {
            var serviceName = Path.GetFileName(serviceDir);
            var srcFolder = Path.Combine(serviceDir, "src");

            Directory.Exists(srcFolder).Should().BeTrue(
                "Service {0} should have a src folder", serviceName);

            if (Directory.Exists(srcFolder))
            {
                var sourceProjects = Directory.GetDirectories(srcFolder)
                    .Where(d => Path.GetFileName(d).StartsWith(serviceName))
                    .ToList();

                sourceProjects.Should().NotBeEmpty(
                    "Service {0} should have at least one source project in src/ folder", serviceName);

                foreach (var projectPath in sourceProjects)
                {
                    var projectName = Path.GetFileName(projectPath);
                    var csprojPath = Path.Combine(projectPath, $"{projectName}.csproj");
                    File.Exists(csprojPath).Should().BeTrue(
                        "Service source project {0} should have a .csproj file", projectName);
                }
            }
        }
    }

    [Fact]
    public void ServiceLayer_TestProjects_ShouldBeInTestsFolder()
    {
        // Arrange - only check services that are in the solution file
        var serviceDirectories = GetSolutionServiceDirectories();

        // Act & Assert
        foreach (var serviceDir in serviceDirectories)
        {
            var serviceName = Path.GetFileName(serviceDir);
            var testsFolder = Path.Combine(serviceDir, "tests");

            if (!Directory.Exists(testsFolder))
            {
                // It's okay if a service doesn't have tests yet (though not recommended)
                continue;
            }

            var testProjects = Directory.GetDirectories(testsFolder)
                .Where(d =>
                {
                    var name = Path.GetFileName(d);
                    return name.StartsWith(serviceName) &&
                           (name.EndsWith(".Tests") || name.EndsWith(".IntegrationTests"));
                })
                .ToList();

            foreach (var projectPath in testProjects)
            {
                var projectName = Path.GetFileName(projectPath);
                var csprojPath = Path.Combine(projectPath, $"{projectName}.csproj");
                File.Exists(csprojPath).Should().BeTrue(
                    "Service test project {0} should have a .csproj file", projectName);
            }
        }
    }

    [Fact]
    public void ServiceLayer_TestProjects_ShouldHaveCorrectSuffix()
    {
        // Arrange - only check services that are in the solution file
        var serviceDirectories = GetSolutionServiceDirectories();

        var invalidTestProjects = new List<string>();

        // Act
        foreach (var serviceDir in serviceDirectories)
        {
            var serviceName = Path.GetFileName(serviceDir);
            var testsFolder = Path.Combine(serviceDir, "tests");

            if (!Directory.Exists(testsFolder))
            {
                continue;
            }

            var allTestFolders = Directory.GetDirectories(testsFolder)
                .Where(d => Path.GetFileName(d).StartsWith(serviceName))
                .ToList();

            var invalid = allTestFolders
                .Where(d =>
                {
                    var name = Path.GetFileName(d);
                    return !name.EndsWith(".Tests") && !name.EndsWith(".IntegrationTests") &&
                           !ProjectConventions.IsExemptSupportProject(name);
                })
                .Select(Path.GetFileName)
                .Where(name => name != null)
                .Cast<string>()
                .ToList();

            invalidTestProjects.AddRange(invalid);
        }

        // Assert
        invalidTestProjects.Should().BeEmpty(
            "All service test projects should end with '.Tests' or '.IntegrationTests' (except *.TestData faker libraries). Invalid: {0}",
            string.Join(", ", invalidTestProjects));
    }

    #endregion

    #region Web Layer Structure Tests

    [Fact]
    public void WebLayer_SourceProjects_ShouldBeInSrcFolder()
    {
        // Arrange
        var webRoot = Path.Combine(SolutionRoot, "Wayd.Web");
        if (!Directory.Exists(webRoot))
        {
            Assert.Fail("Wayd.Web directory should exist");
            return;
        }

        var srcFolder = Path.Combine(webRoot, "src");

        // Act & Assert
        Directory.Exists(srcFolder).Should().BeTrue("Wayd.Web should have a src folder");

        var expectedProjects = new[]
        {
            "Wayd.Web.Api",
            "wayd.web.reactclient"
        };

        foreach (var projectName in expectedProjects)
        {
            var projectPath = Path.Combine(srcFolder, projectName);
            Directory.Exists(projectPath).Should().BeTrue(
                "Web project {0} should exist in Wayd.Web/src/ folder", projectName);
        }
    }

    #endregion

    #region Architecture Tests Project Exception

    [Fact]
    public void ArchitectureTests_ShouldBeAtRootLevel()
    {
        // Arrange
        var archTestsPath = Path.Combine(SolutionRoot, "Wayd.ArchitectureTests");

        // Act & Assert
        Directory.Exists(archTestsPath).Should().BeTrue(
            "Wayd.ArchitectureTests should exist at solution root level");

        var csprojPath = Path.Combine(archTestsPath, "Wayd.ArchitectureTests.csproj");
        File.Exists(csprojPath).Should().BeTrue(
            "Wayd.ArchitectureTests.csproj should exist");
    }

    [Fact]
    public void ArchitectureTests_ShouldNotHaveSrcOrTestsFolders()
    {
        // Arrange
        var archTestsPath = Path.Combine(SolutionRoot, "Wayd.ArchitectureTests");

        // Act
        var srcFolder = Path.Combine(archTestsPath, "src");
        var testsFolder = Path.Combine(archTestsPath, "tests");

        // Assert
        Directory.Exists(srcFolder).Should().BeFalse(
            "Wayd.ArchitectureTests should not have a 'src' folder - it's the only exception to the src/tests rule");

        Directory.Exists(testsFolder).Should().BeFalse(
            "Wayd.ArchitectureTests should not have a 'tests' folder - it's the only exception to the src/tests rule");
    }

    #endregion
}
