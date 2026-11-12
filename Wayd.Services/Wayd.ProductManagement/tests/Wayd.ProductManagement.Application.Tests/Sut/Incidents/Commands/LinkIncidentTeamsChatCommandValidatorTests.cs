using FluentAssertions;
using Wayd.ProductManagement.Application.Incidents.Commands;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents.Commands;

/// <summary>
/// The rules the validator enforces before a handler ever runs.
/// </summary>
public sealed class LinkIncidentTeamsChatCommandValidatorTests
{
    private readonly LinkIncidentTeamsChatCommandValidator _sut = new();

    [Theory]
    [InlineData("http://teams.microsoft.com/l/chat/1")]
    [InlineData("teams.microsoft.com/l/chat/1")]
    [InlineData("javascript:alert(1)")]
    public void Validate_ShouldRejectALinkThatIsNotAnHttpsUrl(string link)
    {
        // Arrange
        var command = new LinkIncidentTeamsChatCommand(Guid.CreateVersion7(), link, null);

        // Act
        var result = _sut.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(LinkIncidentTeamsChatCommand.Link))
            .Which.ErrorMessage.Should().Be("A Teams chat link must be an https URL.");
    }

    [Theory]
    [InlineData("https://teams.microsoft.com/l/chat/1")]
    [InlineData("HTTPS://teams.microsoft.com/l/chat/1")]
    [InlineData("  https://teams.microsoft.com/l/chat/1  ")]
    public void Validate_ShouldAcceptAnHttpsLink(string link)
    {
        // Arrange
        var command = new LinkIncidentTeamsChatCommand(Guid.CreateVersion7(), link, "Bridge");

        // Act
        var result = _sut.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
