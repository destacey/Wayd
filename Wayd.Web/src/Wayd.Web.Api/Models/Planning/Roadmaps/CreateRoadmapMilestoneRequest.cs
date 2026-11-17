using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using OneOf;

namespace Wayd.Web.Api.Models.Planning.Roadmaps;

public sealed record CreateRoadmapMilestoneRequest : CreateRoadmapItemRequest
{
    /// <summary>
    /// The Milestone date.
    /// </summary>
    public required LocalDate Date { get; set; }

    public CreateRoadmapItemCommand ToCreateRoadmapItemCommand()
    {
        return new CreateRoadmapItemCommand(RoadmapId, OneOf<IUpsertRoadmapActivity, IUpsertRoadmapMilestone, IUpsertRoadmapTimebox>.FromT1(new RoadmapMilestoneAdapter(this)));
    }

    private record RoadmapMilestoneAdapter : IUpsertRoadmapMilestone
    {
        private readonly CreateRoadmapMilestoneRequest _request;

        public RoadmapMilestoneAdapter(CreateRoadmapMilestoneRequest request)
        {
            _request = request;
        }

        public string Name => _request.Name;
        public string? Description => _request.Description;
        public Guid? ParentId => _request.ParentId;
        public string? Color => _request.Color;
        public LocalDate Date => _request.Date;
    }
}

public sealed class CreateRoadmapMilestoneRequestValidator : CustomValidator<CreateRoadmapMilestoneRequest>
{
    public CreateRoadmapMilestoneRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        Include(new CreateRoadmapItemRequestValidator());

        RuleFor(t => t.Date)
            .NotEmpty();
    }
}
