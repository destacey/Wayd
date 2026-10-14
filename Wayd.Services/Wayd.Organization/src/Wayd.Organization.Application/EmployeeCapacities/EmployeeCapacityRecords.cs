namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>Which of an employee's capacity record a command loads, so it loads no more than it changes.</summary>
internal enum CapacityPart
{
    /// <summary>The record alone, for a change to the employee's holiday calendar.</summary>
    None,

    /// <summary>With the employee's allocations to teams.</summary>
    Allocations,

    /// <summary>With the employee's time away.</summary>
    TimeAway,
}

/// <summary>Loading and saving an employee's capacity record, which every command that changes it shares.</summary>
internal static class EmployeeCapacityRecords
{
    public const string ChangedElsewhereError = "The employee's capacity was changed by someone else. Reload it and try again.";

    /// <summary>
    /// The employee's capacity record with the <paramref name="part"/> a command is about to change. When they
    /// have none yet it is new and not tracked: <see cref="Save"/> adds it once it holds something.
    /// </summary>
    /// <remarks>
    /// The aggregate checks a change against what is loaded, so a command must ask for the part it changes: a
    /// change to time away made without it would find no other time away to add up with.
    /// </remarks>
    public static async Task<(EmployeeCapacity Capacity, bool IsNew)> LoadOrCreate(IOrganizationDbContext organizationDbContext, Guid employeeId, CapacityPart part, CancellationToken cancellationToken)
    {
        IQueryable<EmployeeCapacity> query = organizationDbContext.EmployeeCapacities;
        query = part switch
        {
            CapacityPart.Allocations => query.Include(c => c.Allocations),
            CapacityPart.TimeAway => query.Include(c => c.TimeAway),
            _ => query,
        };

        var capacity = await query.FirstOrDefaultAsync(c => c.Id == employeeId, cancellationToken);

        return capacity is not null
            ? (capacity, false)
            : (EmployeeCapacity.Create(employeeId), true);
    }

    /// <summary>
    /// Saves a change to the employee's capacity record. A call that changed nothing saves nothing, so no
    /// empty record is left behind. Fails with <see cref="ChangedElsewhereError"/> when someone else changed
    /// the record first, including by creating it at the same moment.
    /// </summary>
    public static async Task<Result> Save(IOrganizationDbContext organizationDbContext, EmployeeCapacity capacity, bool isNew, CancellationToken cancellationToken)
    {
        if (capacity.DomainEvents.Count == 0)
            return Result.Success();

        try
        {
            if (isNew)
                await organizationDbContext.EmployeeCapacities.AddAsync(capacity, cancellationToken);

            await organizationDbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ChangedElsewhereError);
        }
        // The record's id is the employee's, so a failed insert of a new one is a second writer's having
        // inserted it first.
        catch (DbUpdateException) when (isNew)
        {
            return Result.Failure(ChangedElsewhereError);
        }
    }
}
