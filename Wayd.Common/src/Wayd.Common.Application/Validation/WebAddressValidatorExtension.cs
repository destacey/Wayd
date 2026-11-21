using Wayd.Common.Domain;

namespace Wayd.Common.Application.Validation;

/// <summary>
/// The validator rule for a URL a user supplies, shared so every field that stores one refuses the same
/// addresses as <see cref="WebAddress.IsHttp"/>.
/// </summary>
public static class WebAddressValidatorExtension
{
    /// <summary>
    /// Requires an absolute <c>http</c> or <c>https</c> address. Null, empty or whitespace passes, so an optional
    /// URL needs nothing more and a required one chains <c>NotEmpty()</c> first.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> IsWebAddress<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(url => string.IsNullOrWhiteSpace(url) || WebAddress.IsHttp(url))
            .WithMessage("'{PropertyName}' must be a full web address starting with http:// or https://.");
}
