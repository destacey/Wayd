using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Common.Application.Interfaces.ExternalWork;

public interface IExternalIteration<TMetadata> where TMetadata : class
{
    int Id { get; }
    string Name { get; }
    IterationType Type { get; }
    LocalDate? Start { get; }
    LocalDate? End { get; }
    IterationState State { get; }
    Guid? TeamId { get; }
    TMetadata Metadata { get; }
}
