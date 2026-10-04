using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Roadmaps;
using Wayd.Common.Domain.Interfaces;
using Wayd.Planning.Domain.Interfaces;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using NodaTime;
using OneOf;

namespace Wayd.Planning.Domain.Models.Roadmaps;

public sealed class Roadmap : BaseAuditableEntity, ILocalSchedule, IHasIdAndKey
{
    /// <summary>
    /// The maximum number of colors that can be configured on a Roadmap. Colors are persisted as a
    /// JSON column, so this guards against an unbounded payload.
    /// </summary>
    public const int MaxColors = 30;

    private readonly List<RoadmapManager> _roadmapManagers = [];
    private readonly List<BaseRoadmapItem> _items = [];
    private readonly List<RoadmapColor> _colors = [];

    private Roadmap() { }

    private Roadmap(string name, string? description, LocalDateRange dateRange, Visibility visibility, RoadmapState state, IEnumerable<Guid> roadmapManagerIds)
    {
        Guard.Against.NullOrEmpty(roadmapManagerIds, nameof(roadmapManagerIds));

        Name = name;
        Description = description;
        DateRange = dateRange;
        Visibility = visibility;
        State = state;

        foreach (var managerId in roadmapManagerIds.Distinct())
        {
            _roadmapManagers.Add(new RoadmapManager(this, managerId));
        }
    }

    /// <summary>
    /// The unique key of the Roadmap.  This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the Roadmap.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The description of the Roadmap.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// The date range of the Roadmap.
    /// </summary>
    public LocalDateRange DateRange
    {
        get;
        private set => field = Guard.Against.Null(value, nameof(DateRange));
    } = default!;

    /// <summary>
    /// The visibility of the Roadmap. If the Roadmap is public, all users can see the Roadmap. Otherwise, only the Roadmap Managers can see the Roadmap.
    /// </summary>
    public Visibility Visibility { get; private set; }

    /// <summary>
    /// The state of the Roadmap.
    /// </summary>
    public RoadmapState State { get; private set; }

    /// <summary>
    /// The managers of the Roadmap. Managers have full control over the Roadmap.
    /// </summary>
    public IReadOnlyList<RoadmapManager> RoadmapManagers => _roadmapManagers.AsReadOnly();

    /// <summary>
    /// The items on the Roadmap.
    /// </summary>
    public IReadOnlyList<BaseRoadmapItem> Items => _items.AsReadOnly();

    /// <summary>
    /// The configured colors for the Roadmap. These define the named colors available when
    /// coloring activities, and drive the timeline legend.
    /// </summary>
    public IReadOnlyList<RoadmapColor> Colors => _colors.AsReadOnly();

    private IReadOnlyList<RoadmapActivity> RootActivities => [.. _items.OfType<RoadmapActivity>().Where(x => x.ParentId is null).OrderBy(x => x.Order)];

    /// <summary>
    /// Updates the Roadmap.
    /// </summary>
    public Result Update(string name, string? description, LocalDateRange dateRange, IEnumerable<Guid> roadmapManagerIds, Visibility visibility, Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        var isManagerResult = CanModify(currentUserEmployeeId);
        if (isManagerResult.IsFailure)
        {
            return isManagerResult;
        }

        var managerIds = roadmapManagerIds.Distinct().ToArray();
        if (!managerIds.Contains(currentUserEmployeeId))
        {
            return Result.Failure("The current user must be a roadmap manager of the Roadmap in order to update it.");
        }

        // Compared after assignment, never against the arguments: the setters trim.
        var previousDetails = new RoadmapDetails(Name, Description);
        var previousDateRange = DateRange;
        var previousVisibility = Visibility;

        var (added, removed) = SyncManagers(managerIds);

        Name = name;
        Description = description;
        DateRange = dateRange;
        Visibility = visibility;

        var details = new RoadmapDetails(Name, Description);
        if (details != previousDetails)
        {
            AddKeyedDomainEvent(() => new RoadmapDetailsUpdatedEvent(Id, Key, details.Name, details.Description, previousDetails, actor, timestamp));
        }

        var newDateRange = DateRange;
        if (newDateRange != previousDateRange)
        {
            AddKeyedDomainEvent(() => new RoadmapDateRangeChangedEvent(Id, Key, previousDateRange, newDateRange, actor, timestamp));
        }

        var newVisibility = Visibility;
        if (newVisibility != previousVisibility)
        {
            AddKeyedDomainEvent(() => new RoadmapVisibilityChangedEvent(Id, Key, previousVisibility, newVisibility, actor, timestamp));
        }

        if (added.Length > 0 || removed.Length > 0)
        {
            Guid[] currentManagerIds = [.. _roadmapManagers.Select(x => x.ManagerId)];
            AddKeyedDomainEvent(() => new RoadmapManagersChangedEvent(Id, Key, added, removed, currentManagerIds, actor, timestamp));
        }

        return Result.Success();
    }

    private (Guid[] Added, Guid[] Removed) SyncManagers(Guid[] roadmapManagerIds)
    {
        Guid[] added = [.. roadmapManagerIds.Where(x => !_roadmapManagers.Any(y => y.ManagerId == x))];
        Guid[] removed = [.. _roadmapManagers.Where(x => !roadmapManagerIds.Contains(x.ManagerId)).Select(x => x.ManagerId)];

        foreach (var managerId in added)
        {
            _roadmapManagers.Add(new RoadmapManager(this, managerId));
        }

        _roadmapManagers.RemoveAll(x => removed.Contains(x.ManagerId));

        return (added, removed);
    }

    /// <summary>
    /// Replaces the Roadmap's configured colors with the provided set. Colors are managed as a
    /// whole: the existing set is discarded and rebuilt from the input. Colors must be distinct by
    /// hex (the color is the natural key), and at most one may be marked as the default.
    /// </summary>
    /// <param name="colors">The desired set of colors.</param>
    /// <param name="currentUserEmployeeId">The employee performing the update.</param>
    public Result UpdateColors(IEnumerable<IUpsertRoadmapColor> colors, Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        var isManagerResult = CanModify(currentUserEmployeeId);
        if (isManagerResult.IsFailure)
        {
            return isManagerResult;
        }

        var incoming = colors.ToArray();

        if (incoming.Length > MaxColors)
        {
            return Result.Failure($"A Roadmap cannot have more than {MaxColors} colors.");
        }

        if (incoming.Count(x => x.IsDefault) > 1)
        {
            return Result.Failure("Only one color can be marked as the default.");
        }

        var normalizedColors = incoming
            .Select(x => x.Color?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.ToUpperInvariant())
            .ToArray();

        if (normalizedColors.Length != normalizedColors.Distinct().Count())
        {
            return Result.Failure("A Roadmap cannot have two colors with the same value.");
        }

        var previousColors = ColorValues();

        _colors.Clear();
        foreach (var color in incoming)
        {
            _colors.Add(new RoadmapColor(color.Color, color.Name, color.Order, color.IsDefault));
        }

        var newColors = ColorValues();
        if (!previousColors.SequenceEqual(newColors))
        {
            AddKeyedDomainEvent(() => new RoadmapColorsChangedEvent(Id, Key, previousColors, newColors, actor, timestamp));
        }

        return Result.Success();
    }

    private RoadmapColorValues[] ColorValues() =>
        [.. _colors.OrderBy(c => c.Order).Select(c => new RoadmapColorValues(c.Color, c.Name, c.Order, c.IsDefault))];

    /// <summary>
    /// Sets the order of the root child Roadmap Items. This is used to set the order of all child roadmap items at once.
    /// </summary>
    public Result SetChildrenOrder(Dictionary<Guid, int> childActivities, Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        var isManagerResult = CanModify(currentUserEmployeeId);
        if (isManagerResult.IsFailure)
        {
            return isManagerResult;
        }

        if (childActivities.Count != RootActivities.Count)
        {
            return Result.Failure("Not all root roadmap items provided were found.");
        }

        var before = SnapshotItems();

        foreach (var child in RootActivities)
        {
            if (!childActivities.TryGetValue(child.Id, out int order))
            {
                return Result.Failure("Not all child roadmaps provided were found.");
            }

            if (order < 1)
            {
                return Result.Failure("Order must be greater than 0.");
            }

            child.SetOrder(order);
        }

        ResetRootActivitiesOrder();

        RaiseItemChanges(before, null, actor, timestamp);

        return Result.Success();
    }

    /// <summary>
    /// Updates the order of the Roadmap Activities based on a single Roadmap Activity changing its order.
    /// </summary>
    public Result SetActivityOrder(Guid activityId, int order, Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        if (order < 1)
        {
            return Result.Failure("Order must be greater than 0.");
        }

        var isManagerResult = CanModify(currentUserEmployeeId);
        if (isManagerResult.IsFailure)
        {
            return isManagerResult;
        }

        var before = SnapshotItems();

        var result = ApplyActivityOrder(activityId, order);
        if (result.IsSuccess)
        {
            RaiseItemChanges(before, activityId, actor, timestamp);
        }

        return result;
    }

    private Result ApplyActivityOrder(Guid activityId, int order)
    {
        var updatedActivityResult = GetActivity(activityId);
        if (updatedActivityResult.IsFailure)
        {
            return Result.Failure(updatedActivityResult.Error);
        }

        var updatedActivity = updatedActivityResult.Value;
        if (updatedActivity.Order == order)
        {
            return Result.Success();
        }

        if (updatedActivity.ParentId.HasValue)
        {
            var parentActivityResult = GetActivity(updatedActivity.ParentId.Value);
            if (parentActivityResult.IsFailure)
            {
                return Result.Failure(parentActivityResult.Error);
            }

            return parentActivityResult.Value.SetChildActivityOrder(updatedActivity, order);
        }

        if (updatedActivity.Order < order)
        {
            foreach (var child in RootActivities.Where(x => x.Order > updatedActivity.Order && x.Order <= order))
            {
                child.SetOrder(child.Order - 1);
            }
        }
        else
        {
            foreach (var child in RootActivities.Where(x => x.Order >= order && x.Order < updatedActivity.Order))
            {
                child.SetOrder(child.Order + 1);
            }
        }

        updatedActivity.SetOrder(order);

        ResetRootActivitiesOrder();

        return Result.Success();
    }

    /// <summary>
    /// Resets the order of the root Roadmap Activities. This is used to remove any gaps in the order.
    /// </summary>
    private void ResetRootActivitiesOrder()
    {
        int i = 1;
        foreach (var roadmap in RootActivities)
        {
            roadmap.SetOrder(i);
            i++;
        }
    }

    /// <summary>
    /// Raises the deletion event, refusing a caller who cannot modify the Roadmap. The caller removes the
    /// Roadmap in the same save, which is what drains the event.
    /// </summary>
    public Result Delete(Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        var canModifyResult = CanModify(currentUserEmployeeId);
        if (canModifyResult.IsFailure)
        {
            return canModifyResult;
        }

        AddDomainEvent(new RoadmapDeletedEvent(Id, Key, Name, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Archives the Roadmap. Only active roadmaps can be archived.
    /// </summary>
    public Result Archive(Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        if (!IsManager(currentUserEmployeeId))
            return Result.Failure("User is not a roadmap manager of this roadmap.");

        if (State != RoadmapState.Active)
            return Result.Failure("Only active roadmaps can be archived.");

        State = RoadmapState.Archived;

        AddKeyedDomainEvent(() => new RoadmapArchivedEvent(Id, Key, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Activates an archived Roadmap.
    /// </summary>
    public Result Activate(Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        if (!IsManager(currentUserEmployeeId))
            return Result.Failure("User is not a roadmap manager of this roadmap.");

        if (State != RoadmapState.Archived)
            return Result.Failure("Only archived roadmaps can be activated.");

        State = RoadmapState.Active;

        AddKeyedDomainEvent(() => new RoadmapActivatedEvent(Id, Key, actor, timestamp));

        return Result.Success();
    }

    private bool IsManager(Guid employeeId) => _roadmapManagers.Any(x => x.ManagerId == employeeId);

    /// <summary>
    /// Can the employee manage the Roadmap.
    /// </summary>
    private Result CanEmployeeManage(Guid employeeId)
    {
        return IsManager(employeeId)
            ? Result.Success()
            : Result.Failure("User is not a roadmap manager of this roadmap.");
    }

    /// <summary>
    /// Can the employee modify the Roadmap.  The Roadmap must be active and the employee must be a manager.
    /// </summary>
    private Result CanModify(Guid employeeId)
    {
        if (State == RoadmapState.Archived)
            return Result.Failure("Archived roadmaps cannot be modified.");

        return CanEmployeeManage(employeeId);
    }


    #region Roadmap Items Get/Create/Update/Delete

    public Result<BaseRoadmapItem> GetItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId);
        return item is not null
            ? item
            : Result.Failure<BaseRoadmapItem>("Roadmap Item does not exist on this roadmap.");
    }

    private Result<T> CreateRoadmapItem<T>(
        IUpsertRoadmapItem newItem,
        Guid currentUserEmployeeId,
        EventActor actor,
        Instant timestamp,
        Func<Guid, IUpsertRoadmapItem, T> createRootFunc,
        Func<RoadmapActivity, IUpsertRoadmapItem, T> createChildFunc)
        where T : BaseRoadmapItem
    {
        try
        {
            var isManagerResult = CanModify(currentUserEmployeeId);
            if (isManagerResult.IsFailure)
            {
                return Result.Failure<T>(isManagerResult.Error);
            }

            var before = SnapshotItems();

            T item;
            if (newItem.ParentId.HasValue)
            {
                var parentActivityResult = GetActivity(newItem.ParentId.Value);
                if (parentActivityResult.IsFailure)
                {
                    return Result.Failure<T>(parentActivityResult.Error);
                }
                item = createChildFunc(parentActivityResult.Value, newItem);
            }
            else
            {
                item = createRootFunc(Id, newItem);
            }

            _items.Add(item);

            RaiseItemChanges(before, item.Id, actor, timestamp);

            return item;
        }
        catch (Exception ex)
        {
            return Result.Failure<T>(ex.Message);
        }
    }

    public Result<RoadmapActivity> CreateActivity(IUpsertRoadmapActivity newActivity, Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        return CreateRoadmapItem<RoadmapActivity>(
            newActivity,
            currentUserEmployeeId,
            actor,
            timestamp,
            (roadmapId, item) => RoadmapActivity.CreateRoot(roadmapId, (IUpsertRoadmapActivity)item, RootActivities.Count + 1),
            (parent, item) => parent.CreateChildActivity((IUpsertRoadmapActivity)item)
        );
    }

    public Result<RoadmapMilestone> CreateMilestone(IUpsertRoadmapMilestone newMilestone, Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        return CreateRoadmapItem<RoadmapMilestone>(
            newMilestone,
            currentUserEmployeeId,
            actor,
            timestamp,
            (roadmapId, item) => RoadmapMilestone.Create(roadmapId, null, (IUpsertRoadmapMilestone)item),
            (parent, item) => parent.CreateChildMilestone((IUpsertRoadmapMilestone)item)
        );
    }

    public Result<RoadmapTimebox> CreateTimebox(IUpsertRoadmapTimebox newTimebox, Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        return CreateRoadmapItem<RoadmapTimebox>(
            newTimebox,
            currentUserEmployeeId,
            actor,
            timestamp,
            (roadmapId, item) => RoadmapTimebox.Create(roadmapId, null, (IUpsertRoadmapTimebox)item),
            (parent, item) => parent.CreateChildTimebox((IUpsertRoadmapTimebox)item)
        );
    }

    private Result UpdateRoadmapItem<T>(
        Guid itemId,
        IUpsertRoadmapItem item,
        Guid currentUserEmployeeId,
        EventActor actor,
        Instant timestamp,
        Func<T, IUpsertRoadmapItem, RoadmapActivity?, Result> updateFunc)
    where T : BaseRoadmapItem
    {
        try
        {
            var isManagerResult = CanModify(currentUserEmployeeId);
            if (isManagerResult.IsFailure)
            {
                return isManagerResult;
            }

            var roadmapItem = _items.OfType<T>().FirstOrDefault(x => x.Id == itemId);
            if (roadmapItem is null)
            {
                // switch against the type of the item to provide a readable type name
                var typeName = typeof(T).Name switch
                {
                    nameof(RoadmapActivity) => "Roadmap Activity",
                    nameof(RoadmapMilestone) => "Roadmap Milestone",
                    nameof(RoadmapTimebox) => "Roadmap Timebox",
                    _ => "Roadmap Item"
                };

                return Result.Failure($"{typeName} does not exist on this roadmap.");
            }

            RoadmapActivity? parentActivity = null;
            if (item.ParentId.HasValue)
            {
                var parentActivityResult = GetActivity(item.ParentId.Value);
                if (parentActivityResult.IsFailure)
                {
                    return Result.Failure(parentActivityResult.Error);
                }
                parentActivity = parentActivityResult.Value;
            }

            var before = SnapshotItems();

            var parentChanged = item.ParentId != roadmapItem.ParentId;
            var updateRootActivitiesOrder = parentChanged && (item.ParentId is null || roadmapItem.ParentId is null);

            var updateResult = updateFunc(roadmapItem, item, parentActivity);
            if (updateResult.IsFailure)
            {
                return updateResult;
            }

            if (updateRootActivitiesOrder && typeof(T) == typeof(RoadmapActivity))
            {
                ResetRootActivitiesOrder();
            }

            RaiseItemChanges(before, itemId, actor, timestamp);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public Result UpdateActivity(Guid itemId, IUpsertRoadmapActivity activity, Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        return UpdateRoadmapItem<RoadmapActivity>(
            itemId,
            activity,
            currentUserEmployeeId,
            actor,
            timestamp,
            (roadmapActivity, item, parent) => roadmapActivity.Update((IUpsertRoadmapActivity)item, parent)
        );
    }

    public Result UpdateMilestone(Guid itemId, IUpsertRoadmapMilestone milestone, Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        return UpdateRoadmapItem<RoadmapMilestone>(
            itemId,
            milestone,
            currentUserEmployeeId,
            actor,
            timestamp,
            (roadmapMilestone, item, parent) => roadmapMilestone.Update((IUpsertRoadmapMilestone)item, parent)
        );
    }

    public Result UpdateTimebox(Guid itemId, IUpsertRoadmapTimebox timebox, Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        return UpdateRoadmapItem<RoadmapTimebox>(
            itemId,
            timebox,
            currentUserEmployeeId,
            actor,
            timestamp,
            (roadmapTimebox, item, parent) => roadmapTimebox.Update((IUpsertRoadmapTimebox)item, parent)
        );
    }

    /// <summary>
    /// Updates the date(s) of a Roadmap Item. The type of date update is determined by the OneOf parameter.
    /// </summary>
    public Result UpdateRoadmapItemDates(
        Guid itemId,
        OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange> dateUpdate,
        Guid currentUserEmployeeId,
        EventActor actor,
        Instant timestamp)
    {
        var isManagerResult = CanModify(currentUserEmployeeId);
        if (isManagerResult.IsFailure)
            return isManagerResult;

        var item = _items.FirstOrDefault(x => x.Id == itemId);
        if (item is null)
            return Result.Failure("Roadmap Item does not exist on this roadmap.");

        var before = SnapshotItems();

        var result = dateUpdate.Match<Result>(
            activityDateRange =>
                item is RoadmapActivity activity
                    ? activity.UpdateDateRange(activityDateRange)
                    : Result.Failure("Item is not a Roadmap Activity."),
            milestoneDate =>
                item is RoadmapMilestone milestone
                    ? milestone.UpdateDate(milestoneDate)
                    : Result.Failure("Item is not a Roadmap Milestone."),
            timeboxDateRange =>
                item is RoadmapTimebox timebox
                    ? timebox.UpdateDateRange(timeboxDateRange)
                    : Result.Failure("Item is not a Roadmap Timebox.")
        );

        if (result.IsSuccess)
        {
            RaiseItemChanges(before, itemId, actor, timestamp);
        }

        return result;
    }

    /// <summary>
    /// Moves an Activity to a new parent Activity and sets the new order.
    /// </summary>
    public Result MoveActivity(Guid activityId, Guid? newParentId, int newOrder, Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        var isManagerResult = CanModify(currentUserEmployeeId);
        if (isManagerResult.IsFailure)
        {
            return isManagerResult;
        }

        var activityResult = GetActivity(activityId);
        if (activityResult.IsFailure)
        {
            return Result.Failure(activityResult.Error);
        }

        var activity = activityResult.Value;
        if (activity.ParentId == newParentId)
        {
            // TODO: this currently does not support changing the order of the same parent
            return Result.Success();
        }

        if (!newParentId.HasValue && newOrder < 1)
        {
            return Result.Failure("Order must be greater than 0.");
        }

        var before = SnapshotItems();

        var oldParentId = activity.ParentId;

        // Handle moving to a new parent
        if (newParentId.HasValue)
        {
            var newParentResult = GetActivity(newParentId.Value);
            if (newParentResult.IsFailure)
            {
                return Result.Failure(newParentResult.Error);
            }

            var newParent = newParentResult.Value;
            var changeParentResult = activity.ChangeParent(newParent);
            if (changeParentResult.IsFailure)
            {
                return changeParentResult;
            }

            var setChildActivityOrderResult = newParent.SetChildActivityOrder(activity, newOrder);
            if (setChildActivityOrderResult.IsFailure)
            {
                return setChildActivityOrderResult;
            }
        }
        else
        {
            // Handle moving to root
            var changeParentResult = activity.ChangeParent(null);
            if (changeParentResult.IsFailure)
            {
                return changeParentResult;
            }

            activity.SetOrder(RootActivities.Count);

            var setOrderResult = ApplyActivityOrder(activityId, newOrder);
            if (setOrderResult.IsFailure)
            {
                return setOrderResult;
            }
        }

        // Handle removing from old parent
        if (oldParentId.HasValue)
        {
            var oldParentResult = GetActivity(oldParentId.Value);
            if (oldParentResult.IsFailure)
            {
                return Result.Failure(oldParentResult.Error);
            }
            oldParentResult.Value.ResetChildActivitiesOrder();
        }
        else
        {
            ResetRootActivitiesOrder();
        }

        RaiseItemChanges(before, activityId, actor, timestamp);

        return Result.Success();
    }

    public Result DeleteItem(Guid itemId, Guid currentUserEmployeeId, EventActor actor, Instant timestamp)
    {
        var isManagerResult = CanModify(currentUserEmployeeId);
        if (isManagerResult.IsFailure)
        {
            return isManagerResult;
        }

        var roadmapItem = _items.FirstOrDefault(x => x.Id == itemId);
        if (roadmapItem is null)
        {
            return Result.Failure("Roadmap Item does not exist on this roadmap.");
        }

        var before = SnapshotItems();
        var (itemType, itemName) = (roadmapItem.Type, roadmapItem.Name);

        var updateRootActivitiesOrder = !roadmapItem.ParentId.HasValue && roadmapItem.Type == RoadmapItemType.Activity;
        if (roadmapItem.ParentId.HasValue)
        {
            var changeParentResult = roadmapItem.ChangeParent(null);
            if (changeParentResult.IsFailure)
            {
                return changeParentResult;
            }
        }

        RoadmapItemReference[] descendants = [];
        if (roadmapItem is RoadmapActivity activity)
        {
            var itemIdsToRemove = activity.GetSelfAndDescendants();
            var itemsToRemove = _items.Where(x => itemIdsToRemove.Contains(x.Id)).ToList();
            descendants = [.. itemsToRemove.Where(x => x.Id != itemId).Select(x => new RoadmapItemReference(x.Id, x.Type))];
            foreach (var item in itemsToRemove)
            {
                _items.Remove(item);
            }

            if (updateRootActivitiesOrder)
            {
                ResetRootActivitiesOrder();
            }
        }
        else
        {
            _items.Remove(roadmapItem);
        }

        AddKeyedDomainEvent(() => new RoadmapItemDeletedEvent(Id, Key, itemId, itemType, itemName, descendants, actor, timestamp));
        RaiseItemChanges(before, null, actor, timestamp);

        return Result.Success();
    }

    private Result<RoadmapActivity> GetActivity(Guid activityId)
    {
        var activity = _items.OfType<RoadmapActivity>().FirstOrDefault(x => x.Id == activityId);

        return activity is not null
            ? activity
            : Result.Failure<RoadmapActivity>("Roadmap Activity does not exist on this roadmap.");
    }

    #endregion Roadmap Items Create/Update/Delete

    #region Item change events

    /// <summary>
    /// An item as it stood before a change, compared with the item after it to find what the change did.
    /// </summary>
    private sealed record ItemState(Guid? ParentId, int? Order, LocalDateRange DateRange, RoadmapItemDetails Details);

    private Dictionary<Guid, ItemState> SnapshotItems() =>
        _items.ToDictionary(i => i.Id, i => new ItemState(i.ParentId, OrderOf(i), DatesOf(i), DetailsOf(i)));

    private static int? OrderOf(BaseRoadmapItem item) => (item as RoadmapActivity)?.Order;

    private static RoadmapItemDetails DetailsOf(BaseRoadmapItem item) => new(item.Name, item.Description, item.Color);

    private static LocalDateRange DatesOf(BaseRoadmapItem item) => item switch
    {
        RoadmapActivity activity => activity.DateRange,
        RoadmapTimebox timebox => timebox.DateRange,
        RoadmapMilestone milestone => new LocalDateRange(milestone.Date, milestone.Date),
        _ => throw new InvalidOperationException($"Unsupported roadmap item type {item.GetType().Name}.")
    };

    /// <summary>
    /// Raises an event for everything a change did to the items, by comparing them with how they stood
    /// before it. One edit moves more than the item it was made to — a shifted activity takes its subtree,
    /// a grown child widens its ancestors, a reorder renumbers siblings — and this records all of it.
    /// </summary>
    /// <param name="before">The items as they stood before the change.</param>
    /// <param name="changedItemId">The item the change was made to, listed first among the date changes.</param>
    private void RaiseItemChanges(Dictionary<Guid, ItemState> before, Guid? changedItemId, EventActor actor, Instant timestamp)
    {
        foreach (var item in _items.Where(i => !before.ContainsKey(i.Id)))
        {
            RaiseItemAdded(item, actor, timestamp);
        }

        var survivors = _items.Where(i => before.ContainsKey(i.Id)).ToArray();

        foreach (var item in survivors)
        {
            var previous = before[item.Id];
            var details = DetailsOf(item);
            if (details != previous.Details)
            {
                var (itemId, itemType) = (item.Id, item.Type);
                AddKeyedDomainEvent(() => new RoadmapItemDetailsUpdatedEvent(Id, Key, itemId, itemType, details.Name, details.Description, details.Color, previous.Details, actor, timestamp));
            }
        }

        foreach (var item in survivors)
        {
            var previous = before[item.Id];
            if (item.ParentId != previous.ParentId)
            {
                var (itemId, itemType, parentId, order) = (item.Id, item.Type, item.ParentId, OrderOf(item));
                AddKeyedDomainEvent(() => new RoadmapItemMovedEvent(Id, Key, itemId, itemType, previous.ParentId, parentId, previous.Order, order, actor, timestamp));
            }
        }

        RoadmapItemDateChange[] dateChanges = [.. survivors
            .Where(i => DatesOf(i) != before[i.Id].DateRange)
            .OrderBy(i => i.Id == changedItemId ? 0 : 1)
            .Select(i => new RoadmapItemDateChange(i.Id, i.Type, before[i.Id].DateRange, DatesOf(i)))];
        if (dateChanges.Length > 0)
        {
            AddKeyedDomainEvent(() => new RoadmapItemDatesChangedEvent(Id, Key, dateChanges, actor, timestamp));
        }

        RaiseActivityReorders(before, actor, timestamp);
    }

    /// <summary>
    /// Raises a reorder for each parent whose activities changed their order relative to one another. An
    /// activity arriving or leaving renumbers its siblings without reordering them, and is recorded by its
    /// own added, moved or deleted event.
    /// </summary>
    private void RaiseActivityReorders(Dictionary<Guid, ItemState> before, EventActor actor, Instant timestamp)
    {
        // Guid.Empty stands for the root, since a dictionary key cannot be null.
        var previousByParent = before
            .Where(x => x.Value.Order.HasValue)
            .GroupBy(x => x.Value.ParentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Value.Order).Select(x => x.Key).ToArray());
        var currentByParent = _items.OfType<RoadmapActivity>()
            .GroupBy(a => a.ParentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.Order).Select(a => a.Id).ToArray());

        foreach (var (parentKey, current) in currentByParent)
        {
            if (!previousByParent.TryGetValue(parentKey, out var previous))
                continue;

            var stayed = current.Intersect(previous).ToHashSet();
            if (previous.Where(stayed.Contains).SequenceEqual(current.Where(stayed.Contains)))
                continue;

            Guid? parentId = parentKey == Guid.Empty ? null : parentKey;
            AddKeyedDomainEvent(() => new RoadmapActivitiesReorderedEvent(Id, Key, parentId, previous, current, actor, timestamp));
        }
    }

    private void RaiseItemAdded(BaseRoadmapItem item, EventActor actor, Instant timestamp)
    {
        var (itemId, name, description, parentId, color) = (item.Id, item.Name, item.Description, item.ParentId, item.Color);
        switch (item)
        {
            case RoadmapActivity activity:
                var (activityRange, order) = (activity.DateRange, activity.Order);
                AddKeyedDomainEvent(() => new RoadmapActivityAddedEvent(Id, Key, itemId, name, description, parentId, color, activityRange, order, actor, timestamp));
                break;
            case RoadmapMilestone milestone:
                var date = milestone.Date;
                AddKeyedDomainEvent(() => new RoadmapMilestoneAddedEvent(Id, Key, itemId, name, description, parentId, color, date, actor, timestamp));
                break;
            case RoadmapTimebox timebox:
                var timeboxRange = timebox.DateRange;
                AddKeyedDomainEvent(() => new RoadmapTimeboxAddedEvent(Id, Key, itemId, name, description, parentId, color, timeboxRange, actor, timestamp));
                break;
        }
    }

    #endregion Item change events

    /// <summary>
    /// Creates a new Roadmap.
    /// </summary>
    public static Result<Roadmap> Create(string name, string? description, LocalDateRange dateRange, Visibility visibility, IEnumerable<Guid> roadmapManagerIds, EventActor actor, Instant timestamp)
    {
        try
        {
            var roadmap = new Roadmap(name, description, dateRange, visibility, RoadmapState.Active, roadmapManagerIds);
            roadmap.RaiseCreated(actor, timestamp);
            return roadmap;
        }
        catch (Exception ex)
        {
            return Result.Failure<Roadmap>(ex.Message);
        }
    }

    /// <summary>
    /// Creates a copy of an existing Roadmap with a new name, managers, and visibility.
    /// </summary>
    /// <param name="name">The name for the new roadmap.</param>
    /// <param name="roadmapManagerIds">The managers for the new roadmap.</param>
    /// <param name="visibility">The visibility for the new roadmap.</param>
    /// <returns>A new Roadmap that is a copy of the current one.</returns>
    public Result<Roadmap> Copy(string name, IEnumerable<Guid> roadmapManagerIds, Visibility visibility, EventActor actor, Instant timestamp)
    {
        try
        {
            var newRoadmap = new Roadmap(name, Description, DateRange, visibility, RoadmapState.Active, roadmapManagerIds);

            // Copy all items - we need to maintain a mapping of old items to new items for parent references
            var itemMapping = new Dictionary<Guid, BaseRoadmapItem>();

            // First pass: copy all items without parent relationships
            foreach (var item in _items)
            {
                BaseRoadmapItem newItem;

                switch (item)
                {
                    case RoadmapActivity activity:
                        newItem = new RoadmapActivity(
                            newRoadmap.Id,
                            activity.Name,
                            activity.Description,
                            activity.DateRange,
                            null, // Will set parent in second pass
                            activity.Color,
                            activity.Order);
                        break;
                    case RoadmapMilestone milestone:
                        newItem = new RoadmapMilestone(
                            newRoadmap.Id,
                            milestone.Name,
                            milestone.Description,
                            milestone.Date,
                            null, // Will set parent in second pass
                            milestone.Color);
                        break;
                    case RoadmapTimebox timebox:
                        newItem = new RoadmapTimebox(
                            newRoadmap.Id,
                            timebox.Name,
                            timebox.Description,
                            timebox.DateRange,
                            null, // Will set parent in second pass
                            timebox.Color);
                        break;
                    default:
                        continue;
                }

                // Map old item ID to new item instance (not ID, since new IDs aren't generated yet)
                itemMapping[item.Id] = newItem;
                newRoadmap._items.Add(newItem);
            }

            // Second pass: update parent references using the item mapping
            foreach (var oldItem in _items)
            {
                if (!oldItem.ParentId.HasValue) continue;

                // Find the corresponding new item
                if (!itemMapping.TryGetValue(oldItem.Id, out var newItem)) continue;

                // Find the new parent item
                if (!itemMapping.TryGetValue(oldItem.ParentId.Value, out var newParent)) continue;

                // Only activities can be parents
                if (newParent is RoadmapActivity newParentActivity)
                {
                    newItem.SetParentDirect(newParentActivity);
                }
            }

            newRoadmap.RaiseCreated(actor, timestamp);

            return newRoadmap;
        }
        catch (Exception ex)
        {
            return Result.Failure<Roadmap>(ex.Message);
        }
    }

    /// <summary>
    /// Raises the creation event once the first save has assigned <see cref="Key"/>. Everything else is
    /// captured now, so a caller that changes the roadmap before that save does not rewrite its creation.
    /// </summary>
    private void RaiseCreated(EventActor actor, Instant timestamp)
    {
        var (name, description, dateRange, visibility, state) = (Name, Description, DateRange, Visibility, State);
        Guid[] managerIds = [.. _roadmapManagers.Select(m => m.ManagerId)];
        var colors = ColorValues();
        RoadmapItemValues[] items = [.. _items.Select(i => new RoadmapItemValues(
            i.Id, i.Type, i.Name, i.Description, i.ParentId, i.Color, DatesOf(i), OrderOf(i)))];

        AddPostPersistenceAction(() => AddDomainEvent(new RoadmapCreatedEvent(
            Id, Key, name, description, dateRange, visibility, state, managerIds, colors, items, actor, timestamp)));
    }

    /// <summary>
    /// Raises an event that carries <see cref="Key"/>, waiting for the first save to assign it.
    /// </summary>
    /// <remarks>
    /// The factory runs when the event is raised, so everything else it carries must be captured in locals by
    /// the caller — only Key may be read inside it.
    /// </remarks>
    private void AddKeyedDomainEvent(Func<DomainEvent> build)
    {
        if (Key == 0)
            AddPostPersistenceAction(() => AddDomainEvent(build()));
        else
            AddDomainEvent(build());
    }
}
