namespace Wayd.Common.Application.Auditing;

public sealed record GetMyAuditLogsQuery : IQuery<List<AuditDto>>
{
}

public sealed class GetMyAuditLogsQueryHandler : IQueryHandler<GetMyAuditLogsQuery, List<AuditDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public GetMyAuditLogsQueryHandler(ICurrentUser currentUser, IAuditService auditService) =>
        (_currentUser, _auditService) = (currentUser, auditService);

    public Task<List<AuditDto>> Handle(GetMyAuditLogsQuery request, CancellationToken cancellationToken) =>
        _auditService.GetUserTrailsAsync(_currentUser.GetUserId());
}