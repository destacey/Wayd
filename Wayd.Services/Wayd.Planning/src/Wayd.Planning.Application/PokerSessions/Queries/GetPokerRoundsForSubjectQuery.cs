using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Extensions;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.PokerSessions.Queries;

/// <summary>
/// The poker rounds that estimated a record, such as an intake request, in the sessions the caller can see.
/// </summary>
/// <remarks>
/// A round in a session the caller cannot see is absent, never a redacted row, so the list cannot disclose a
/// private session. Planning does not check the record itself: knowing its id shows only rounds the caller could
/// already open.
/// </remarks>
/// <param name="Kind">The kind of record.</param>
/// <param name="Id">The record's id in the module that owns it.</param>
public sealed record GetPokerRoundsForSubjectQuery(PokerRoundSubjectKind Kind, Guid Id) : IQuery<IReadOnlyList<PokerSubjectRoundDto>>;

public sealed class GetPokerRoundsForSubjectQueryValidator : CustomValidator<GetPokerRoundsForSubjectQuery>
{
    public GetPokerRoundsForSubjectQueryValidator()
    {
        RuleFor(q => q.Kind).IsInEnum();
        RuleFor(q => q.Id).NotEmpty();
    }
}

/// <summary>
/// Reads the record's rounds through the session access filter, newest session first.
/// </summary>
public sealed class GetPokerRoundsForSubjectQueryHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access)
    : IQueryHandler<GetPokerRoundsForSubjectQuery, IReadOnlyList<PokerSubjectRoundDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;

    public async Task<IReadOnlyList<PokerSubjectRoundDto>> Handle(GetPokerRoundsForSubjectQuery request, CancellationToken cancellationToken)
    {
        var reader = await _access.ResolveReader(cancellationToken);

        var rows = await _planningDbContext.PokerSessions
            .Where(reader.Filter)
            .SelectMany(s => s.Rounds
                .Where(r => r.Subject != null && r.Subject.Kind == request.Kind && r.Subject.Id == request.Id)
                .Select(r => new
                {
                    SessionId = s.Id,
                    SessionKey = s.Key,
                    SessionName = s.Name,
                    RoundId = r.Id,
                    r.Order,
                    r.Status,
                    r.ConsensusEstimate,
                    // Its values rather than the publication itself: an owned value cannot be read without the
                    // round that owns it.
                    IsPublished = r.Publication != null,
                    PublicationOutcome = (PokerRoundPublicationOutcome?)r.Publication!.Outcome,
                    PublicationAt = (Instant?)r.Publication!.At,
                    PublicationByEmployeeId = (Guid?)r.Publication!.ByEmployeeId,
                    PublicationRefusalReason = r.Publication!.RefusalReason,
                }))
            .OrderByDescending(x => x.SessionKey)
            .ThenBy(x => x.Order)
            .ThenBy(x => x.RoundId)
            .ToListAsync(cancellationToken);

        var publisherIds = rows.Where(x => x.IsPublished).Select(x => x.PublicationByEmployeeId!.Value).Distinct().ToList();
        var publishers = publisherIds.Count == 0
            ? []
            : await _planningDbContext.Employees
                .Where(e => publisherIds.Contains(e.Id))
                .ProjectToType<EmployeeNavigationDto>()
                .ToDictionaryAsync(e => e.Id, cancellationToken);

        return [.. rows.Select(x => new PokerSubjectRoundDto
        {
            Session = new NavigationDto { Id = x.SessionId, Key = x.SessionKey, Name = x.SessionName },
            RoundId = x.RoundId,
            Status = x.Status.GetDisplayName(),
            ConsensusEstimate = x.ConsensusEstimate,
            Publication = !x.IsPublished
                ? null
                : new PokerRoundPublicationDto
                {
                    Outcome = x.PublicationOutcome!.Value,
                    At = x.PublicationAt!.Value,
                    ByEmployeeId = x.PublicationByEmployeeId!.Value,
                    By = publishers.GetValueOrDefault(x.PublicationByEmployeeId!.Value),
                    RefusalReason = x.PublicationRefusalReason,
                },
        })];
    }
}
