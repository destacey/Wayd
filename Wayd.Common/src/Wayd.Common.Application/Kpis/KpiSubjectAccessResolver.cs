using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis;

/// <summary>
/// Answers what the caller may do with one record's KPIs: the record's own rule, widened by the KPI administrator's
/// permission and narrowed by a read-only token.
/// </summary>
/// <remarks>
/// The one path every KPI command and query takes, so the waiver and the token limit are applied in one place and no
/// subject type can forget them.
/// </remarks>
public sealed class KpiSubjectAccessResolver(IEnumerable<IKpiSubjectType> subjectTypes, ICurrentPrincipal currentPrincipal)
{
    /// <summary>The permission that manages every record's KPIs and the organization's KPI categories.</summary>
    public static readonly string AdministerPermission =
        ApplicationPermission.NameFor(ApplicationAction.Administer, ApplicationResource.Kpis);

    private readonly IEnumerable<IKpiSubjectType> _subjectTypes = subjectTypes;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    /// <summary>
    /// What the caller may do with the record's KPIs, under the registered spelling of its subject type. Fails when
    /// the subject type is not registered or no module answers for it; null when the caller cannot read the record or
    /// it does not exist.
    /// </summary>
    public async Task<Result<ResolvedKpiSubject?>> Resolve(string? subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var descriptor = KpiSubjectTypes.Resolve(subjectType);
        if (descriptor.IsFailure)
            return Result.Failure<ResolvedKpiSubject?>(descriptor.Error);

        var implementation = _subjectTypes.FirstOrDefault(t =>
            string.Equals(t.SubjectType, descriptor.Value.Key, StringComparison.OrdinalIgnoreCase));
        if (implementation is null)
            return Result.Failure<ResolvedKpiSubject?>(KpiErrors.UnknownSubjectType(descriptor.Value.Key));

        var access = await implementation.Resolve(subjectId, cancellationToken);
        if (access is null)
            return Result.Success<ResolvedKpiSubject?>(null);

        var canManage = !_currentPrincipal.IsReadOnly
            && (access.CanManage || await _currentPrincipal.HasPermission(AdministerPermission, cancellationToken));

        return Result.Success<ResolvedKpiSubject?>(new ResolvedKpiSubject(descriptor.Value, subjectId, access with { CanManage = canManage }));
    }

    /// <summary>Whether the caller may manage the organization's KPI categories.</summary>
    public async Task<bool> CanManageCategories(CancellationToken cancellationToken) =>
        !_currentPrincipal.IsReadOnly && await _currentPrincipal.HasPermission(AdministerPermission, cancellationToken);
}

/// <summary>
/// A record whose KPIs the caller can read, with its registered subject type and what the caller may do.
/// </summary>
public sealed record ResolvedKpiSubject(KpiSubjectTypeDescriptor Descriptor, Guid SubjectId, KpiSubjectAccess Access)
{
    /// <summary>The registered spelling of the subject type, the one KPIs are stored under.</summary>
    public string SubjectType => Descriptor.Key;
}
