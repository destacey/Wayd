using Wayd.Work.Application.WorkTypeLevels.Commands;

namespace Wayd.Web.Api.Models.Work.WorkTypeLevels;

public sealed record UpdateWorkTypeLevelRequest
{
    public int Id { get; set; }

    /// <summary>The work type level's new name, unique among levels.</summary>
    public string Name { get; set; } = default!;

    /// <summary>The description of the work type level.</summary>
    public string? Description { get; set; }

    public UpdateWorkTypeLevelCommand ToUpdateWorkTypeLevelCommand()
    {
        return new UpdateWorkTypeLevelCommand(Id, Name, Description);
    }
}

public sealed class UpdateWorkTypeLevelRequestValidator : CustomValidator<UpdateWorkTypeLevelRequest>
{
    public UpdateWorkTypeLevelRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Description)
            .MaximumLength(1024);
    }
}
