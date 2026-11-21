using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.AppIntegrations.Connections;

namespace Wayd.Web.Api.Tests.Sut.Models.AppIntegrations.Connections;

public sealed class InitWorkspaceIntegrationRequestValidatorTests
{
    private readonly InitWorkspaceIntegrationRequestValidator _validator = new();

    private static InitWorkspaceIntegrationRequest Request(string? template) => new()
    {
        Id = Guid.CreateVersion7(),
        ExternalId = Guid.CreateVersion7(),
        WorkspaceKey = "WEB",
        WorkspaceName = "Web",
        ExternalViewWorkItemUrlTemplate = template,
    };

    [Theory]
    [InlineData("https://dev.azure.com/org/project/_workitems/edit/")]
    [InlineData(null)]
    public void Validate_AcceptsAWebAddressOrNone(string? template)
    {
        // Act
        var result = _validator.TestValidate(Request(template));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_RefusesATemplateThatIsNotAWebAddress()
    {
        // Act
        var result = _validator.TestValidate(Request("javascript:alert(1)//"));

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.ExternalViewWorkItemUrlTemplate);
    }
}
