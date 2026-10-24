using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request's need-by date with everything that travels with it, as an event records it.
/// </summary>
/// <param name="Date">The date the request is needed by.</param>
/// <param name="Basis">What makes it that date.</param>
/// <param name="Reference">What the date rests on: the regulation and clause, the contract and clause, the event, or a note.</param>
/// <param name="CommittedById">For a <see cref="NeedByBasis.Commitment"/>, the employee who made it; otherwise null.</param>
/// <param name="CommittedOn">For a <see cref="NeedByBasis.Commitment"/>, the date it was made; otherwise null.</param>
public sealed record IntakeRequestNeedByDetails(
    LocalDate Date,
    NeedByBasis Basis,
    string Reference,
    Guid? CommittedById,
    LocalDate? CommittedOn);
