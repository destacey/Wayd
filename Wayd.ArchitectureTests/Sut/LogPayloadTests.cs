using System.Text.RegularExpressions;
using FluentAssertions;
using Wayd.ArchitectureTests.Helpers;

namespace Wayd.ArchitectureTests.Sut;

/// <summary>
/// Keeps whole requests, events and other objects out of log events and failure strings.
/// </summary>
/// <remarks>
/// Commands carry credentials, personal data and free text, and a failure string reaches the API's caller
/// as well as the log. Destructuring (<c>{@X}</c>) and interpolating a record both write every property,
/// and a property pushed onto <c>LogContext</c> lands on every event in its scope. Handlers log a request's
/// name and the ids that locate the failure instead; see <c>docs/contributing/coding-standards.mdx#logging</c>.
/// </remarks>
public class LogPayloadTests
{
    private static IEnumerable<string> ProductionSourceFiles()
    {
        var solutionRoot = AssemblyHelper.GetSolutionRoot();

        return Directory.GetFiles(solutionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !SegmentsOf(f).Any(s =>
                s is "bin" or "obj" or "tests" or ".claude" or "node_modules" or ".next"
                || s.EndsWith("Tests", StringComparison.Ordinal)
                || s.EndsWith("TestData", StringComparison.Ordinal)));
    }

    private static string[] SegmentsOf(string path) =>
        path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

    private static List<string> FilesMatching(Regex pattern, Func<string, bool>? exempt = null) =>
        ProductionSourceFiles()
            .Select(f => Path.GetRelativePath(AssemblyHelper.GetSolutionRoot(), f))
            .Where(f => exempt is null || !exempt(f))
            .Where(f => pattern.IsMatch(File.ReadAllText(Path.Combine(AssemblyHelper.GetSolutionRoot(), f))))
            .ToList();

    [Fact]
    public void NoLogTemplateDestructuresAnObject()
    {
        // Arrange — a plain template only: in an interpolated string, {@event.Id} is C#'s escaped identifier
        var destructures = new Regex(@"(?<![$@])""[^""\r\n]*\{@\w+(:[^}]*)?\}", RegexOptions.Compiled);

        // Act
        var offenders = FilesMatching(destructures);

        // Assert
        offenders.Should().BeEmpty(
            "{@X} writes every property of X into the log. Log the request's name and the ids that locate "
            + "the failure instead.");
    }

    [Fact]
    public void NoStringInterpolatesAWholeRequestOrEvent()
    {
        // Arrange
        var interpolates = new Regex(
            @"\$""[^""\r\n]*\{(request|command|notification|@event|redactedRequest)\}",
            RegexOptions.Compiled);

        // Act
        var offenders = FilesMatching(interpolates);

        // Assert
        offenders.Should().BeEmpty(
            "interpolating a record calls its ToString, which prints every property, and a failure string "
            + "reaches the API's caller as well as the log. Interpolate the request's name instead.");
    }

    [Fact]
    public void OnlyInfrastructurePushesOntoTheLogContext()
    {
        // Arrange — the request and job middleware push correlation and user ids for every event they wrap
        var pushes = new Regex(@"LogContext\.PushProperty\s*\(", RegexOptions.Compiled);

        // Act
        var offenders = FilesMatching(pushes, f => f.StartsWith("Wayd.Infrastructure", StringComparison.Ordinal));

        // Assert
        offenders.Should().BeEmpty(
            "a property pushed onto LogContext lands on every event in its scope, and a handler that pushes "
            + "its request or event logs the whole object. Log the ids in the message template instead.");
    }
}
