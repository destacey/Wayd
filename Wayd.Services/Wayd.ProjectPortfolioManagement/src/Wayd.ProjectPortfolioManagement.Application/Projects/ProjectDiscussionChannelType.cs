using Wayd.Common.Application.Discussions;
using Wayd.Common.Domain.Discussions;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.ProjectPortfolioManagement.Application.Projects;

/// <summary>
/// Who may read, post and moderate in a project's conversation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reading and posting.</strong> Anyone who can read the project, by <see cref="ProjectReadRule"/>'s rule,
/// reads the conversation; anyone with a linked employee posts, whatever the project's status. A read-only token
/// writes nothing.
/// </para>
/// <para>
/// <strong>Moderating</strong> — locking, deleting anyone's message — is delivery leadership: Owner or Manager on the
/// project, its program or its portfolio, or the PPM administrator, by the rule <c>Project.CanManageProject</c> applies to every change to the project.
/// </para>
/// <para>
/// <strong>No internal notes</strong>, and a mention only notifies: project access comes from permissions and roles,
/// never from a conversation, so posting changes nothing on the project.
/// </para>
/// </remarks>
public sealed class ProjectDiscussionChannelType(
    IProjectPortfolioManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal) : IDiscussionChannelType
{
    private readonly IProjectPortfolioManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    /// <inheritdoc/>
    public string ChannelType => PpmDiscussionChannels.Project.Key;

    /// <inheritdoc/>
    public async Task<DiscussionAccess?> Resolve(Guid ownerId, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HasPermission(ProjectReadRule.ViewPermission, cancellationToken))
            return null;

        // Null for a read-only token, so it neither posts nor moderates.
        var (employeeId, isPpmAdministrator) = await _currentPrincipal.ResolveManagementHint(cancellationToken);
        if (employeeId is not { } id)
        {
            return await _dbContext.Projects.AnyAsync(p => p.Id == ownerId, cancellationToken)
                ? new DiscussionAccess(CanReadInternal: false, CanPost: false, CanPostInternal: false, CanModerate: false)
                : null;
        }

        // The ancestors' roles must be loaded: without them the check silently finds no inherited leadership and
        // refuses a program's or portfolio's leader.
        var project = await _dbContext.Projects
            .AsNoTracking()
            .Include(p => p.Roles)
            .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
            .Include(p => p.Program).ThenInclude(p => p!.Roles)
            .FirstOrDefaultAsync(p => p.Id == ownerId, cancellationToken);
        if (project is null)
            return null;

        var ancestry = project.AncestryRoles();
        var isLeader = isPpmAdministrator || project.CanManageProject(id, ancestry.PortfolioRoles, ancestry.ProgramRoles);

        return new DiscussionAccess(CanReadInternal: false, CanPost: true, CanPostInternal: false, CanModerate: isLeader);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Any active employee: a mention grants nothing, and a notification about a project its recipient cannot read
    /// is never shown. Nothing in an internal note, which a project never holds.
    /// </remarks>
    public async Task<IReadOnlySet<Guid>> MentionableAmong(
        Guid ownerId,
        IReadOnlyCollection<Guid> employeeIds,
        DiscussionMessageKind kind,
        CancellationToken cancellationToken)
    {
        if (kind != DiscussionMessageKind.Public || employeeIds.Count == 0)
            return new HashSet<Guid>();

        var active = await _dbContext.Employees
            .Where(e => employeeIds.Contains(e.Id) && e.IsActive)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        return active.ToHashSet();
    }

    /// <inheritdoc/>
    /// <remarks>Nothing: posting never changes who has the project.</remarks>
    public Task OnPosted(DiscussionChannel channel, DiscussionMessage message, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    /// <remarks>Nothing: a mention never changes who has the project.</remarks>
    public Task OnEdited(
        DiscussionChannel channel,
        DiscussionMessage message,
        IReadOnlyCollection<Guid> addedMentionIds,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    /// <remarks>Always null: a project's conversations have no settings to change.</remarks>
    public Task<DiscussionSettingsAccess?> ResolveSettings(Guid? scopeId, CancellationToken cancellationToken) =>
        Task.FromResult<DiscussionSettingsAccess?>(null);
}
