using Serilog;
using Serilog.Events;
using Wayd.Infrastructure.Logging;
using Wolverine;

namespace Wayd.Infrastructure.Tests.Sut.Logging;

public sealed class ExceptionDetailsTests
{
    private const string Secret = "pat_s3cr3t-value";

    [Fact]
    public void WithWaydExceptionDetails_LeavesOutAnEnvelopeHeldByTheException()
    {
        // Arrange
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration()
            .Enrich.WithWaydExceptionDetails()
            .WriteTo.Sink(sink)
            .CreateLogger();
        var exception = new EnvelopeBearingException(new Envelope(new SecretBearingCommand(Secret)));

        // Act
        logger.Error(exception, "Handling failed");

        // Assert
        var details = (DictionaryValue)sink.Events.Should().ContainSingle().Subject.Properties["ExceptionDetail"];
        details.Elements.Select(e => e.Key.Value).Should().Contain("Reason").And.NotContain("Duplicates");
        details.ToString().Should().NotContain(Secret);
    }

    [Fact]
    public void ApiConfiguration_DoesNotRegisterTheEnricherAgain()
    {
        // Arrange
        var api = Path.Combine(RepositoryRoot(), "Wayd.Web", "src", "Wayd.Web.Api");
        var files = Directory.GetFiles(Path.Combine(api, "Configurations"), "*.json")
            .Concat(Directory.GetFiles(api, "appsettings*.json"))
            .Append(Path.Combine(RepositoryRoot(), "docker-compose.yml"))
            .ToList();

        // Act
        var registering = files.Where(f => File.ReadAllText(f).Contains("WithExceptionDetails")).ToList();

        // Assert
        files.Should().Contain(f => f.EndsWith("logger.json"));
        registering.Should().BeEmpty("ExceptionDetails registers the enricher in code, and a second registration can log what it leaves out");
    }

    /// <summary>Walks up from the test binary until the repository root is in hand.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wayd.slnx")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the tests run from inside the repository");

        return directory!.FullName;
    }

    private sealed record SecretBearingCommand(string AccessToken);

    private sealed class EnvelopeBearingException(Envelope duplicate) : Exception("duplicate delivery")
    {
        public IReadOnlyList<Envelope> Duplicates { get; } = [duplicate];

        public string Reason => "duplicate";
    }
}
