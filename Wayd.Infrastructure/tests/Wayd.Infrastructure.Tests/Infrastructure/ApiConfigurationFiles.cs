using Microsoft.Extensions.Configuration;

namespace Wayd.Infrastructure.Tests.Infrastructure;

/// <summary>
/// Reads the API's checked-in configuration files from the source tree, so a test of a startup guard runs against
/// the values a developer's host actually boots with rather than a copy that can drift from them.
/// </summary>
internal static class ApiConfigurationFiles
{
    /// <summary>
    /// Loads one file from <c>Wayd.Web/src/Wayd.Web.Api/Configurations/</c>, such as <c>security.Development.json</c>.
    /// </summary>
    public static IConfiguration Load(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wayd.slnx")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the tests run from inside the repository");
        var path = Path.Combine(directory!.FullName, "Wayd.Web", "src", "Wayd.Web.Api", "Configurations", fileName);
        return new ConfigurationBuilder().AddJsonFile(path, optional: false).Build();
    }
}
