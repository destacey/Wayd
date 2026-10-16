using Wayd.Work.Application.WorkTypes.Commands;

namespace Wayd.Web.Api.Models.Work.WorkTypes;

public sealed record UpdateWorkTypeRequest
{
    public int Id { get; set; }

    /// <summary>The description of the work type.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// The work type level identifier.
    /// </summary>
    public int LevelId { get; set; }

    public UpdateWorkTypeCommand ToUpdateWorkTypeCommand()
    {
        return new UpdateWorkTypeCommand(Id, Description, LevelId);
    }
}

public sealed class UpdateWorkTypeRequestValidator : CustomValidator<UpdateWorkTypeRequest>
{
    public UpdateWorkTypeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Description)
            .MaximumLength(1024);

        RuleFor(c => c.LevelId)
            .GreaterThan(0);
    }
}
