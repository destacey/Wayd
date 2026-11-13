using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.PokerSessions.Queries;

/// <summary>
/// The poker sessions the caller can see: the standalone sessions of the main poker grid, or the sessions one host
/// holds.
/// </summary>
/// <param name="HostKind">The kind of host whose sessions to list, given together with <paramref name="HostId"/>.</param>
/// <param name="HostId">The host whose sessions to list; without it, only standalone sessions are listed.</param>
public sealed record GetPokerSessionsQuery(
    PokerSessionStatus? Status = null,
    PokerSessionHostKind? HostKind = null,
    Guid? HostId = null) : IQuery<IReadOnlyList<PokerSessionListDto>>;

/// <summary>
/// Refuses a host's kind without its id, or the reverse, which would name no host.
/// </summary>
public sealed class GetPokerSessionsQueryValidator : CustomValidator<GetPokerSessionsQuery>
{
    public GetPokerSessionsQueryValidator()
    {
        RuleFor(q => q.HostKind).IsInEnum();
        RuleFor(q => q)
            .Must(q => q.HostKind.HasValue == q.HostId.HasValue)
            .WithMessage("Give a host's kind and id together, or neither.");
    }
}

/// <summary>
/// Lists the poker sessions the caller can see, newest first. A hosted session never appears among the standalone
/// ones, whoever is asking.
/// </summary>
public sealed class GetPokerSessionsQueryHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access) : IQueryHandler<GetPokerSessionsQuery, IReadOnlyList<PokerSessionListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;

    public async Task<IReadOnlyList<PokerSessionListDto>> Handle(GetPokerSessionsQuery request, CancellationToken cancellationToken)
    {
        var reader = await _access.ResolveReader(cancellationToken);

        var query = _planningDbContext.PokerSessions.Where(reader.Filter);

        if (request.HostKind is { } hostKind && request.HostId is { } hostId)
            query = query.Where(s => s.Host != null && s.Host.Kind == hostKind && s.Host.Id == hostId);
        else
            query = query.Where(s => s.Host == null);

        if (request.Status.HasValue)
            query = query.Where(s => s.Status == request.Status.Value);

        var sessions = await query
            .OrderByDescending(s => s.Key)
            .ProjectToType<PokerSessionListDto>()
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
            session.CanManage = reader.CanManage(session.Facilitators.Select(f => f.Id), session.Host?.Id);

        return sessions;
    }
}
