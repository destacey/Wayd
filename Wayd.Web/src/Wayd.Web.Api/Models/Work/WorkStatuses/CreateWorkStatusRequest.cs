namespace Wayd.Web.Api.Models.Work.WorkStatuses;

public sealed record CreateWorkStatusRequest
{
    /// <summary>The name of the work status.  The name cannot be changed.</summary>
    public string Name { get; set; } = default!;

    /// <summary>The description of the work status.</summary>
    public string? Description { get; set; }

    public CreateWorkStatusCommand ToCreateWorkStatusCommand()
    {
        return new CreateWorkStatusCommand(Name, Description);
    }
}

public sealed class CreateWorkStatusRequestValidator : CustomValidator<CreateWorkStatusRequest>
{
    public CreateWorkStatusRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(c => c.Description)
            .MaximumLength(1024);
    }
}
