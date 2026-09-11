using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A portfolio's name or description was edited.
/// </summary>
/// <remarks>
/// Carries the details after the change, in the shape <see cref="ProjectPortfolioCreatedEvent"/> uses, so
/// the two compare directly, and the details it replaced as <see cref="Previous"/>, grouped the way the
/// project and program details events group them.
/// </remarks>
public sealed record ProjectPortfolioDetailsUpdatedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectPortfolioDetailsUpdatedEvent(
        Guid id,
        int key,
        string name,
        string description,
        ProjectPortfolioDetails previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string Description { get; }

    /// <summary>The details this edit replaced.</summary>
    public ProjectPortfolioDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "ProjectPortfolio";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
