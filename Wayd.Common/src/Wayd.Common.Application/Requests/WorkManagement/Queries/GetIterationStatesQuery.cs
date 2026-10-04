using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Common.Application.Requests.WorkManagement.Queries;

/// <summary>
/// The state of each iteration now, keyed by id. State follows each sprint's actual and default dates in its
/// team's zone and is never stored, so a module keeping a copy of a sprint reads it here. An id that matches no
/// iteration is missing from the result.
/// </summary>
public sealed record GetIterationStatesQuery(IReadOnlyCollection<Guid> Ids) : IQuery<IReadOnlyDictionary<Guid, IterationStateDto>>;

/// <param name="ActiveFrom">
/// When the sprint became Active: when the team started it, or else the start of its first planned day in
/// <paramref name="TimeZone"/>. Null, with the other two, for an iteration that is not a mapped team's sprint
/// with planned dates.
/// </param>
/// <param name="ActiveUntil">When the sprint stops being Active, exclusive.</param>
/// <param name="TimeZone">The IANA zone the sprint's days are counted in.</param>
public sealed record IterationStateDto(IterationState State, Instant? ActiveFrom, Instant? ActiveUntil, string? TimeZone);
