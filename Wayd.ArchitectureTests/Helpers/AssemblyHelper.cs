namespace Wayd.ArchitectureTests.Helpers;

/// <summary>
/// Provides helper methods for loading and accessing assemblies in architecture tests.
/// This centralizes assembly loading logic to ensure consistency across all test classes.
/// </summary>
public static class AssemblyHelper
{
    #region Domain Assembly Methods

    /// <summary>
    /// Gets all Domain assemblies (Wayd.*.Domain, including Common.Domain).
    /// </summary>
    public static System.Reflection.Assembly[] GetDomainAssemblies()
    {
        var assemblyDir = GetAssemblyDirectory();
        var domainDlls = Directory.GetFiles(assemblyDir, "Wayd.*.Domain.dll")
            .Where(f =>
            {
                var fileName = Path.GetFileName(f);
                return !fileName.Contains("Test");
            })
            .ToArray();

        return domainDlls.Select(System.Reflection.Assembly.LoadFrom).ToArray();
    }

    /// <summary>
    /// Gets all service Domain assemblies (excludes Common.Domain).
    /// Pattern: Wayd.{ServiceName}.Domain
    /// </summary>
    public static System.Reflection.Assembly[] GetServiceDomainAssemblies()
    {
        var assemblyDir = GetAssemblyDirectory();
        var domainDlls = Directory.GetFiles(assemblyDir, "Wayd.*.Domain.dll")
            .Where(f =>
            {
                var fileName = Path.GetFileName(f);
                return !fileName.Contains("Common.Domain") && !fileName.Contains("Test");
            })
            .ToArray();

        return domainDlls.Select(System.Reflection.Assembly.LoadFrom).ToArray();
    }

    #endregion

    #region Application Assembly Methods

    /// <summary>
    /// Gets all Application assemblies (Wayd.*.Application, including Common.Application).
    /// </summary>
    public static System.Reflection.Assembly[] GetApplicationAssemblies()
    {
        var assemblyDir = GetAssemblyDirectory();
        var applicationDlls = Directory.GetFiles(assemblyDir, "Wayd.*.Application.dll")
            .Where(f =>
            {
                var fileName = Path.GetFileName(f);
                return !fileName.Contains("Test");
            })
            .ToArray();

        return applicationDlls.Select(System.Reflection.Assembly.LoadFrom).ToArray();
    }

    /// <summary>
    /// Gets all service Application assemblies (excludes Common.Application).
    /// Pattern: Wayd.{ServiceName}.Application
    /// </summary>
    public static System.Reflection.Assembly[] GetServiceApplicationAssemblies()
    {
        var assemblyDir = GetAssemblyDirectory();
        var applicationDlls = Directory.GetFiles(assemblyDir, "Wayd.*.Application.dll")
            .Where(f =>
            {
                var fileName = Path.GetFileName(f);
                return !fileName.Contains("Common.Application") && !fileName.Contains("Test");
            })
            .ToArray();

        return applicationDlls.Select(System.Reflection.Assembly.LoadFrom).ToArray();
    }

    #endregion

    #region Infrastructure Assembly Methods

    /// <summary>
    /// Gets all Infrastructure assemblies.
    /// </summary>
    public static System.Reflection.Assembly[] GetInfrastructureAssemblies()
    {
        var assemblyDir = GetAssemblyDirectory();
        var infrastructureDlls = Directory.GetFiles(assemblyDir, "Wayd.*.Infrastructure*.dll")
            .Where(f =>
            {
                var fileName = Path.GetFileName(f);
                return !fileName.Contains("Test");
            })
            .ToArray();

        return infrastructureDlls.Select(System.Reflection.Assembly.LoadFrom).ToArray();
    }

    #endregion

    #region Integration Assembly Methods

    /// <summary>
    /// Gets all Integration assemblies.
    /// Pattern: Wayd.Integrations.{IntegrationName}
    /// </summary>
    public static System.Reflection.Assembly[] GetIntegrationAssemblies()
    {
        var assemblyDir = GetAssemblyDirectory();
        var integrationDlls = Directory.GetFiles(assemblyDir, "Wayd.Integrations.*.dll")
            .Where(f =>
            {
                var fileName = Path.GetFileName(f);
                return !fileName.Contains("Test");
            })
            .ToArray();

        return integrationDlls.Select(System.Reflection.Assembly.LoadFrom).ToArray();
    }

    #endregion

    #region Common Assembly Methods

    /// <summary>
    /// Gets the Wayd.Common.Domain assembly.
    /// </summary>
    public static System.Reflection.Assembly GetCommonDomainAssembly()
    {
        return typeof(Wayd.Common.Domain.Events.DomainEvent).Assembly;
    }

    /// <summary>
    /// Gets the Wayd.Common.Application assembly.
    /// </summary>
    public static System.Reflection.Assembly GetCommonApplicationAssembly()
    {
        return typeof(Wayd.Common.Application.Persistence.IWaydDbContext).Assembly;
    }

    /// <summary>
    /// Gets the Wayd.Common assembly.
    /// </summary>
    public static System.Reflection.Assembly GetCommonAssembly()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Wayd.Common");

        if (assembly == null)
        {
            throw new InvalidOperationException("Wayd.Common assembly not found");
        }

        return assembly;
    }

    #endregion

    #region Specific Assembly Methods

    /// <summary>
    /// Gets the Wayd.Infrastructure assembly.
    /// </summary>
    public static System.Reflection.Assembly GetInfrastructureAssembly()
    {
        return typeof(Wayd.Infrastructure.Persistence.Context.WaydDbContext).Assembly;
    }

    #endregion

    #region File System Helpers

    /// <summary>
    /// Gets the solution root directory.
    /// </summary>
    public static string GetSolutionRoot()
    {
        // Start from the test assembly location and walk up to find the solution root
        var assemblyLocation = GetAssemblyDirectory();
        var currentDir = assemblyLocation;

        while (currentDir != null)
        {
            // Look for Wayd.slnx file
            if (File.Exists(Path.Combine(currentDir, "Wayd.slnx")))
            {
                return currentDir;
            }

            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        throw new InvalidOperationException("Could not find solution root. Expected to find Wayd.slnx file.");
    }

    /// <summary>
    /// Gets the directory where test assemblies are located.
    /// </summary>
    public static string GetAssemblyDirectory()
    {
        var assembly = typeof(AssemblyHelper).Assembly;
        var assemblyLocation = Path.GetDirectoryName(assembly.Location);

        if (string.IsNullOrEmpty(assemblyLocation))
        {
            throw new InvalidOperationException("Could not determine assembly location");
        }

        return assemblyLocation;
    }

    #endregion

    #region Handler Helper Methods

    /// <summary>
    /// Gets the names of all handler types in the given assemblies.
    /// Useful for checking handler dependencies.
    /// </summary>
    public static string[] GetHandlerTypeNames(System.Reflection.Assembly[] assemblies)
    {
        var handlerTypes = NetArchTest.Rules.Types.InAssemblies(assemblies)
            .That()
            .HaveNameEndingWith("Handler")
            .And()
            .AreClasses()
            .GetTypes();

        return handlerTypes
            .Select(t => t.FullName ?? t.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToArray();
    }

    #endregion
}
