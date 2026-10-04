using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;

namespace Wayd.Common.Domain.Interfaces.Planning.Iterations;

public interface ISimpleIteration
{
    Guid Id { get; }
    int Key { get; }
    string Name { get; }
    IterationType Type { get; }
    IterationDateRange DateRange { get; }
    Guid? TeamId { get; }

    // excludes OwnershipInfo and ExternalMetadata
}
