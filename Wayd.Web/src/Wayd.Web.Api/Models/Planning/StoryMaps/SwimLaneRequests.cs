using NodaTime;
using Wayd.Planning.Application.StoryMaps.Commands;

namespace Wayd.Web.Api.Models.Planning.StoryMaps;

public class AddSwimLaneRequest
{
    public string Name { get; set; } = default!;

    public AddSwimLaneCommand ToCommand(Guid storyMapId) => new(storyMapId, Name);
}

public sealed class AddLaneRequestValidator : CustomValidator<AddSwimLaneRequest>
{
    public AddLaneRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
    }
}

public class RenameSwimLaneRequest
{
    public string Name { get; set; } = default!;

    public RenameSwimLaneCommand ToCommand(Guid storyMapId, Guid swimLaneId) => new(storyMapId, swimLaneId, Name);
}

public sealed class RenameLaneRequestValidator : CustomValidator<RenameSwimLaneRequest>
{
    public RenameLaneRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
    }
}

public class SetSwimLaneDatesRequest
{
    public LocalDate? StartDate { get; set; }
    public LocalDate? EndDate { get; set; }

    public SetSwimLaneDatesCommand ToCommand(Guid storyMapId, Guid swimLaneId) => new(storyMapId, swimLaneId, StartDate, EndDate);
}

public class ReorderSwimLaneRequest
{
    public int NewOrder { get; set; }

    public ReorderSwimLaneCommand ToCommand(Guid storyMapId, Guid swimLaneId) => new(storyMapId, swimLaneId, NewOrder);
}
