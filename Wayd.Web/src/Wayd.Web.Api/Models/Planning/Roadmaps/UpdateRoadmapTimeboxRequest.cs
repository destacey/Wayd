using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Application.Roadmaps.Dtos;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using OneOf;

namespace Wayd.Web.Api.Models.Planning.Roadmaps;

public sealed record UpdateRoadmapTimeboxRequest : UpdateRoadmapItemRequest
{
    /// <summary>
    /// The Roadmap Item start date.
    /// </summary>
    public LocalDate Start { get; set; }

    /// <summary>
    /// The Roadmap Item end date.
    /// </summary>
    public LocalDate End { get; set; }

    /// <summary>
    /// Creates an UpdateRoadmapTimeboxRequest from a RoadmapTimeboxDetailsDto.
    /// </summary>
    public static UpdateRoadmapTimeboxRequest FromDto(RoadmapTimeboxDetailsDto dto)
    {
        return new UpdateRoadmapTimeboxRequest
        {
            RoadmapId = dto.RoadmapId,
            ItemId = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            ParentId = dto.Parent?.Id,
            Color = dto.Color,
            Start = dto.Start,
            End = dto.End
        };
    }

    /// <inheritdoc />
    public override UpdateRoadmapItemCommand ToUpdateRoadmapItemCommand()
    {
        return new UpdateRoadmapItemCommand(RoadmapId, ItemId, OneOf<IUpsertRoadmapActivity, IUpsertRoadmapMilestone, IUpsertRoadmapTimebox>.FromT2(new UpsertRoadmapTimeboxAdapter(this)));
    }
}

public sealed class UpdateRoadmapTimeboxRequestValidator : CustomValidator<UpdateRoadmapTimeboxRequest>
{
    public UpdateRoadmapTimeboxRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        Include(new UpdateRoadmapItemRequestValidator());

        RuleFor(t => t.Start)
            .NotNull();

        RuleFor(t => t.End)
            .NotNull()
            .Must((membership, end) => membership.Start <= end)
                .WithMessage("End date must be greater than or equal to start date");
    }
}
