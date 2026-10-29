using Wayd.Common.Domain.Tagging;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

/// <summary>
/// Files an intake request under a typed name.
/// </summary>
public sealed record TagIntakeRequestByNameRequest
{
    /// <summary>
    /// The tag's name. When exactly one tag on an axis the request's desk offers has it, ignoring case, that tag is
    /// applied; otherwise the free tag with the name is applied, and created when none has it yet.
    /// </summary>
    public string Name { get; set; } = default!;
}

public sealed class TagIntakeRequestByNameRequestValidator : CustomValidator<TagIntakeRequestByNameRequest>
{
    public TagIntakeRequestByNameRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(TagBase.NameMaxLength);
    }
}
