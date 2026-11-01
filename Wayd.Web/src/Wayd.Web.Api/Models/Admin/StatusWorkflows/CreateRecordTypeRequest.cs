using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>A request to define a record type for an owner type that has them.</summary>
public sealed record CreateRecordTypeRequest
{
    /// <summary>The owner type the record type is a variant of, such as <c>work.item</c>.</summary>
    public string OwnerType { get; set; } = default!;

    /// <summary>What the record type is called, such as Bug.</summary>
    public string Name { get; set; } = default!;

    public CreateRecordTypeCommand ToCreateRecordTypeCommand() =>
        new(OwnerType, Name);
}

public sealed class CreateRecordTypeRequestValidator : CustomValidator<CreateRecordTypeRequest>
{
    public CreateRecordTypeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.OwnerType).NotEmpty().MaximumLength(64);
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
    }
}
