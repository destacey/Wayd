using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows;

/// <inheritdoc cref="IStatusHistoryReader"/>
public sealed class StatusHistoryReader(
    IStatusWorkflowDbContext dbContext,
    IWaydDbContext waydDbContext) : IStatusHistoryReader
{
    private readonly IStatusWorkflowDbContext _dbContext = dbContext;
    private readonly IWaydDbContext _waydDbContext = waydDbContext;

    public async Task<Result<List<StatusTransitionDto>>> Read(
        string ownerType, Guid recordId, CancellationToken cancellationToken)
    {
        var owner = WorkflowOwners.Resolve(ownerType);
        if (owner.IsFailure)
        {
            return Result.Failure<List<StatusTransitionDto>>(owner.Error);
        }

        if (recordId == Guid.Empty)
        {
            return Result.Failure<List<StatusTransitionDto>>("A record is required.");
        }

        // Both halves of the key: RecordId is not a foreign key and is only unique within an owner
        // type, so filtering on it alone would mix in another module's record sharing an id.
        var transitions = await _dbContext.StatusTransitions
            .AsNoTracking()
            .Where(t => t.OwnerType == owner.Value.Key && t.RecordId == recordId)
            .OrderByDescending(t => t.Sequence)
            .ToListAsync(cancellationToken);

        if (transitions.Count == 0)
        {
            return new List<StatusTransitionDto>();
        }

        var users = await ResolveUsers(transitions, cancellationToken);
        var employees = await ResolveEmployees(transitions, cancellationToken);

        return transitions.ConvertAll(t => ToDto(t, users, employees));
    }

    /// <summary>
    /// The employees behind these transitions, by id.
    /// </summary>
    /// <remarks>
    /// Looked up rather than joined for the same reason as the users: the read must not drop a
    /// transition whose employee row has since been deactivated or filtered out.
    /// </remarks>
    private async Task<Dictionary<Guid, NavigationDto>> ResolveEmployees(
        List<StatusTransition> transitions, CancellationToken cancellationToken)
    {
        var employeeIds = transitions
            .Where(t => t.ActorEmployeeId.HasValue)
            .Select(t => t.ActorEmployeeId!.Value)
            .Distinct()
            .ToList();

        if (employeeIds.Count == 0)
        {
            return [];
        }

        // Materialized before reading Name.DisplayName, which is computed from the value object's parts
        // and so has no SQL translation.
        var employees = await _waydDbContext.Employees
            .AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        return employees.ToDictionary(
            e => e.Id,
            e => NavigationDto.Create(e.Id, e.Key, e.Name.DisplayName));
    }

    /// <summary>
    /// The accounts behind these transitions, by id.
    /// </summary>
    /// <remarks>
    /// Looked up separately rather than joined, because <see cref="StatusTransition.ActorUserId"/> is
    /// not a foreign key: an id whose account has since been deleted matches nothing, and an inner join
    /// would drop that transition from the history rather than reporting it with no account.
    /// </remarks>
    private async Task<Dictionary<string, UserNavigationDto>> ResolveUsers(
        List<StatusTransition> transitions, CancellationToken cancellationToken)
    {
        var userIds = transitions
            .Select(t => t.ActorUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id) && !SystemIdentity.IsSystem(id))
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
        {
            return [];
        }

        var users = await _waydDbContext.WaydUsers
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.UserName })
            .ToListAsync(cancellationToken);

        // TryParse rather than Parse: the account id is a string column, and one that is not a GUID must
        // leave the transition readable without its account rather than throwing the whole history away.
        return users
            .Where(u => Guid.TryParse(u.Id, out _))
            .ToDictionary(
                u => u.Id,
                u => new UserNavigationDto
                {
                    Id = Guid.Parse(u.Id),
                    UserName = u.UserName,
                    Name = u.DisplayName,
                });
    }

    private static StatusTransitionDto ToDto(
        StatusTransition transition,
        Dictionary<string, UserNavigationDto> users,
        Dictionary<Guid, NavigationDto> employees)
    {
        var isSystem = SystemIdentity.IsSystem(transition.ActorUserId);

        return new StatusTransitionDto
        {
            Id = transition.Id,
            Sequence = transition.Sequence,
            FromStatus = transition.FromStatusId.HasValue
                ? new FrozenStatusDto
                {
                    Id = transition.FromStatusId.Value,
                    Name = transition.FromStatusName!,
                    Category = transition.FromCategory!.Value,
                }
                : null,
            ToStatus = new StatusNavigationDto
            {
                Id = transition.ToStatusId,
                Name = transition.ToStatusName,
                Category = transition.ToCategory,
                Alias = transition.ToAlias,
            },
            WorkflowId = transition.WorkflowId,
            ActorKind = SimpleNavigationDto.FromEnum(transition.ActorKind),
            ChangedBy = transition.ActorEmployeeId.HasValue
                && employees.TryGetValue(transition.ActorEmployeeId.Value, out var employee)
                    ? employee
                    : null,
            ChangedByUser = !isSystem
                && transition.ActorUserId is not null
                && users.TryGetValue(transition.ActorUserId, out var user)
                    ? user
                    : null,
            ChangedBySystem = isSystem,
            ChangedOn = transition.ChangedOn,
            Reason = transition.Reason,
        };
    }
}
