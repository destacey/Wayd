using FluentAssertions;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class IntakeRequestOriginTests
{
    [Fact]
    public void Create_WithSourceAndExternalId_TrimsAndKeepsTheUrl()
    {
        // Act
        var result = IntakeRequestOrigin.Create(" ServicePortal ", " REQ-1042 ", " https://portal.acme.example/req/1042 ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SourceSystem.Should().Be("ServicePortal");
        result.Value.ExternalId.Should().Be("REQ-1042");
        result.Value.Url.Should().Be("https://portal.acme.example/req/1042");
    }

    [Fact]
    public void Create_WithABlankUrl_HasNoUrl()
    {
        // Act
        var result = IntakeRequestOrigin.Create("ServicePortal", "REQ-1042", "  ");

        // Assert
        result.Value.Url.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "REQ-1042")]
    [InlineData("ServicePortal", " ")]
    public void Create_MissingSourceOrExternalId_Fails(string? sourceSystem, string? externalId)
    {
        // Act
        var result = IntakeRequestOrigin.Create(sourceSystem, externalId, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An origin needs both its source system and its external id.");
    }
}
