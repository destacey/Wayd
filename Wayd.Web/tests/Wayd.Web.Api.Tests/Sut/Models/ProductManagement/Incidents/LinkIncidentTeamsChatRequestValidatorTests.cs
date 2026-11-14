using FluentAssertions;
using Wayd.Web.Api.Models.ProductManagement.Incidents;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.Incidents;

/// <summary>
/// The shape the API rejects before the command is dispatched.
/// </summary>
public sealed class LinkIncidentTeamsChatRequestValidatorTests
{
    private readonly LinkIncidentTeamsChatRequestValidator _sut = new();

    [Theory]
    [InlineData("http://teams.microsoft.com/l/chat/1")]
    [InlineData("teams.microsoft.com/l/chat/1")]
    public void Validate_ShouldRejectALinkThatIsNotAnHttpsUrl(string link)
    {
        // Arrange
        var request = new LinkIncidentTeamsChatRequest
        {
            Id = Guid.CreateVersion7(),
            Link = link,
        };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(LinkIncidentTeamsChatRequest.Link))
            .Which.ErrorMessage.Should().Be("A Teams chat link must be an https URL.");
    }

    [Theory]
    [InlineData("https://teams.microsoft.com/l/chat/1")]
    [InlineData("HTTPS://teams.microsoft.com/l/chat/1")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ShouldAcceptAnHttpsLinkOrAnEmptyLink(string? link)
    {
        // Arrange
        var request = new LinkIncidentTeamsChatRequest
        {
            Id = Guid.CreateVersion7(),
            Link = link,
        };

        // Act
        var result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
