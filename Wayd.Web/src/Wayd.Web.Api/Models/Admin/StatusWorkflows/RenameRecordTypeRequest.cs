using Wayd.Common.Application.StatusWorkflows.Commands;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>A request to rename an owned record type.</summary>
public sealed record RenameRecordTypeRequest
{
    /// <summary>The record type's new name.</summary>
    public string Name { get; set; } = default!;

    public RenameRecordTypeCommand ToRenameRecordTypeCommand(Guid id) =>
        new(id, Name);
}

public sealed class RenameRecordTypeRequestValidator : CustomValidator<RenameRecordTypeRequest>
{
    public RenameRecordTypeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
    }
}
