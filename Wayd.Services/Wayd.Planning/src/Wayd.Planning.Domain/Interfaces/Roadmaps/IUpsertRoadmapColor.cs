namespace Wayd.Planning.Domain.Interfaces.Roadmaps;

public interface IUpsertRoadmapColor
{
    string Color { get; }
    string Name { get; }
    int Order { get; }
    bool IsDefault { get; }
}
