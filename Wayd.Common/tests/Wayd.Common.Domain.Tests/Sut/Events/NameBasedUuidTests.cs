using Wayd.Common.Domain.Events;

namespace Wayd.Common.Domain.Tests.Sut.Events;

public sealed class NameBasedUuidTests
{
    [Fact]
    public void Create_MatchesTheRfc9562Example()
    {
        // Arrange — RFC 9562 appendix A.4: the DNS namespace and "www.example.com"
        var dnsNamespace = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        // Act
        var uuid = NameBasedUuid.Create(dnsNamespace, "www.example.com");

        // Assert
        uuid.Should().Be(new Guid("2ed6657d-e927-568b-95e1-2665a8aea6a2"));
        uuid.Version.Should().Be(5);
    }

    [Fact]
    public void Create_IsDeterministic()
    {
        // Arrange
        var namespaceId = Guid.CreateVersion7();

        // Act
        var first = NameBasedUuid.Create(namespaceId, "Project:alpha");
        var second = NameBasedUuid.Create(namespaceId, "Project:alpha");

        // Assert
        first.Should().Be(second);
    }
}
