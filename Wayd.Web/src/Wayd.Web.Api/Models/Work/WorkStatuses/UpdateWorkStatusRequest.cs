namespace Wayd.Web.Api.Models.Work.WorkStatuses;

public sealed record UpdateWorkStatusRequest
{
    public int Id { get; set; }

    /// <summary>The description of the work status.</summary>
    public string? Description { get; set; }

    public UpdateWorkStatusCommand ToUpdateWorkStatusCommand()
    {
        return new UpdateWorkStatusCommand(Id, Description);
    }
}

public sealed class UpdateWorkStatusRequestValidator : CustomValidator<UpdateWorkStatusRequest>
{
    public UpdateWorkStatusRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Description)
            .MaximumLength(1024);
    }
}
