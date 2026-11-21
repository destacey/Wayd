using FluentValidation;
using FluentValidation.TestHelper;
using Wayd.Common.Application.Validation;

namespace Wayd.Common.Application.Tests.Sut.Validators;

public sealed class WebAddressValidatorExtensionTests
{
    private sealed record Target(string? Url);

    private sealed class TargetValidator : AbstractValidator<Target>
    {
        public TargetValidator() => RuleFor(t => t.Url).IsWebAddress();
    }

    private readonly TargetValidator _validator = new();

    [Theory]
    [InlineData("https://example.com/page")]
    [InlineData("http://example.com")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void IsWebAddress_PassesAWebAddressOrNothing(string? url)
    {
        // Act
        var result = _validator.TestValidate(new Target(url));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("example.com/page")]
    public void IsWebAddress_RefusesAnythingElse(string url)
    {
        // Act
        var result = _validator.TestValidate(new Target(url));

        // Assert
        result.ShouldHaveValidationErrorFor(t => t.Url)
            .WithErrorMessage("'Url' must be a full web address starting with http:// or https://.");
    }
}
