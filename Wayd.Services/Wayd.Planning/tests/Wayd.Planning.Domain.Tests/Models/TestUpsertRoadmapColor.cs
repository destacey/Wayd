using Wayd.Planning.Domain.Interfaces.Roadmaps;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Domain.Tests.Models;

internal record TestUpsertRoadmapColor : IUpsertRoadmapColor
{
    public TestUpsertRoadmapColor() { }

    public TestUpsertRoadmapColor(RoadmapColor color)
    {
        Color = color.Color;
        Name = color.Name;
        Order = color.Order;
        IsDefault = color.IsDefault;
    }

    public string Color { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int Order { get; set; }
    public bool IsDefault { get; set; }
}
