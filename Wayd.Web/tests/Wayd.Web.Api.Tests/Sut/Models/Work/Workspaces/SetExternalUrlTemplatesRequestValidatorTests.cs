using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.Work.Workspaces;

namespace Wayd.Web.Api.Tests.Sut.Models.Work.Workspaces;

public sealed class SetExternalUrlTemplatesRequestValidatorTests
{
    private readonly SetExternalUrlTemplatesRequestValidator _validator = new();

    [Theory]
    [InlineData("https://dev.azure.com/org/project/_workitems/edit/")]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_AcceptsAWebAddressOrAClear(string? template)
    {
        // Act
        var result = _validator.TestValidate(new SetExternalUrlTemplatesRequest(template));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("javascript:alert(1)//")]
    [InlineData("dev.azure.com/org/project/_workitems/edit/")]
    public void Validate_RefusesATemplateThatIsNotAWebAddress(string template)
    {
        // Act
        var result = _validator.TestValidate(new SetExternalUrlTemplatesRequest(template));

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.ExternalViewWorkItemUrlTemplate);
    }
}
