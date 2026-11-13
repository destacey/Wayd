using Wayd.Common.Application.Employees.Dtos;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Dtos;

/// <summary>
/// What came of last publishing a poker round's consensus to the record it estimates.
/// </summary>
public sealed record PokerRoundPublicationDto : IMapFrom<PokerRoundPublication>
{
    /// <summary>
    /// <c>Published</c> when the consensus was written to the record; <c>Refused</c> when the record's area
    /// refused it and nothing was written.
    /// </summary>
    public PokerRoundPublicationOutcome Outcome { get; set; }

    /// <summary>When the facilitator published, or tried to.</summary>
    public Instant At { get; set; }

    /// <summary>The id of the employee who published, or tried to.</summary>
    public Guid ByEmployeeId { get; set; }

    /// <summary>
    /// The employee who published, or tried to. Null in a live update, which carries only
    /// <see cref="ByEmployeeId"/>.
    /// </summary>
    public EmployeeNavigationDto? By { get; set; }

    /// <summary>Why the record's area refused the consensus, or null when it was written.</summary>
    public string? RefusalReason { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<PokerRoundPublication, PokerRoundPublicationDto>()
            .Ignore(dest => dest.By!);
    }
}
