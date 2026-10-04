using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Integrations.AzureDevOps.Models.Contracts;

public sealed record AzdoIteration : IExternalIteration<AzdoIterationMetadata>
{
    public AzdoIteration(int id, string name, IterationType type, LocalDate? startDate, LocalDate? endDate, Guid? teamId, AzdoIterationMetadata metadata)
    {
        Id = id;
        Name = name;
        Type = type;
        Start = startDate;
        End = endDate;
        TeamId = teamId;
        Metadata = metadata;
    }

    public int Id { get; init; }
    public string Name { get; init; }
    public IterationType Type { get; init; }
    public LocalDate? Start { get; init; }

    /// <summary>The last planned day, included in the iteration.</summary>
    public LocalDate? End { get; init; }

    public Guid? TeamId { get; init; }
    public AzdoIterationMetadata Metadata { get; init; }
}
