using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Integrations.AzureDevOps.Models.Contracts;

public sealed record AzdoIteration : IExternalIteration<AzdoIterationMetadata>
{
    public AzdoIteration(int id, string name, IterationType type, LocalDate? startDate, LocalDate? endDate, Guid? teamId, AzdoIterationMetadata metadata, Instant timestamp)
    {
        Id = id;
        Name = name;
        Type = type;
        Start = startDate;
        End = endDate;
        TeamId = teamId;
        Metadata = metadata;

        SetState(timestamp.InUtc().Date);
    }

    public int Id { get; init; }
    public string Name { get; init; }
    public IterationType Type { get; init; }
    public LocalDate? Start { get; init; }

    /// <summary>The last planned day, included in the iteration.</summary>
    public LocalDate? End { get; init; }

    public IterationState State { get; private set; }
    public Guid? TeamId { get; init; }
    public AzdoIterationMetadata Metadata { get; init; }

    /// <param name="today">The current date in UTC. An iteration is Active through the whole of its last day.</param>
    private void SetState(LocalDate today)
    {
        var state = IterationState.Unknown;
        if (Start.HasValue && End.HasValue)
        {
            if (Start.Value > today)
            {
                state = IterationState.Future;
            }
            else if (End.Value < today)
            {
                state = IterationState.Completed;
            }
            else
            {
                state = IterationState.Active;
            }
        }
        else if (Start.HasValue && !End.HasValue)
        {
            state = Start.Value > today
                ? IterationState.Future
                : IterationState.Active;
        }
        else if (!Start.HasValue && End.HasValue)
        {
            state = End.Value < today
                ? IterationState.Completed
                : IterationState.Active;
        }

        State = state;
    }
}
