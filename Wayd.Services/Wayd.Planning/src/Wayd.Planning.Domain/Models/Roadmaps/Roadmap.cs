using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Interfaces;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Interfaces;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using OneOf;

namespace Wayd.Planning.Domain.Models.Roadmaps;

public sealed class Roadmap : BaseAuditableEntity, ILocalSchedule, IHasIdAndKey
{
    /// <summary>
    /// The maximum number of colors that can be configured on a Roadmap. Colors are persisted as a
    /// JSON column, so this guards against an unbounded payload.
    /// </summary>
    public const int MaxColors = 30;

    private readonly bool _objectConstruction = false;
    private readonly List<RoadmapManager> _roadmapManagers = [];
    private readonly List<BaseRoadmapItem> _items = [];
    private readonly List<RoadmapColor> _colors = [];

    private Roadmap() { }

    private Roadmap(string name, string? description, LocalDateRange dateRange, Visibility visibility, RoadmapState state, IEnumerable<Guid> roadmapManagerIds)
    {
        _objectConstruction = true;

        Guard.Against.NullOrEmpty(roadmapManagerIds, nameof(roadmapManagerIds));

        Name = name;
        Description = description;
        DateRange = dateRange;
        Visibility = visibility;
        State = state;

        foreach (var managerId in roadmapManagerIds)
        {
            AddManager(managerId, managerId);
        }

        _objectConstruction = false;
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
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="dateRange"></param>
    /// <param name="visibility"></param>
    /// <param name="color"></param>
    /// <param name="currentUserEmployeeId"
    /// <returns></returns>
    public Result Update(string name, string? description, LocalDateRange dateRange, IEnumerable<Guid> roadmapManagerIds, Visibility visibility, Guid currentUserEmployeeId)
    {
        var isManagerResult = CanModify(currentUserEmployeeId);
        if (isManagerResult.IsFailure)
        {
            return isManagerResult;
        }

        if (!roadmapManagerIds.Contains(currentUserEmployeeId))
        {
            return Result.Failure("The current user must be a roadmap manager of the Roadmap in order to update it.");
        }

        var syncManagersResult = SyncManagers(roadmapManagerIds, currentUserEmployeeId);
        if (syncManagersResult.IsFailure)
        {
            return syncManagersResult;
        }

        Name = name;
        Description = description;
        DateRange = dateRange;
        Visibility = visibility;

        return Result.Success();
    }

    private Result SyncManagers(IEnumerable<Guid> roadmapManagerIds, Guid currentUserEmployeeId)
    {
        // TODO: This is a temporary solution to sync managers.
        var managerIdsToAdd = roadmapManagerIds.Where(x => !_roadmapManagers.Any(y => y.ManagerId == x)).ToArray();
        var managerIdsToRemove = _roadmapManagers.Where(x => !roadmapManagerIds.Contains(x.ManagerId)).Select(x => x.ManagerId).ToArray();

        foreach (var managerId in managerIdsToAdd)
        {
            AddManager(managerId, currentUserEmployeeId);
        }

        foreach (var managerId in managerIdsToRemove)
        {
            RemoveManager(managerId, currentUserEmployeeId);
        }

        return Result.Success();
    }

    /// <summary>
    /// Replaces the Roadmap's configured colors with the provided set. Colors are managed as a
    /// whole: the existing set is discarded and rebuilt from the input. Colors must be distinct by
    /// hex (the color is the natural key), and at most one may be marked as the default.
    /// </summary>
    /// <param name="colors">The desired set of colors.</param>
    /// <param name="currentUserEmployeeId">The employee performing the update.</param>
    public Result UpdateColors(IEnumerable<IUpsertRoadmapColor> colors, Guid currentUserEmployeeId)
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

        _colors.Clear();
        foreach (var color in incoming)
        {
            _colors.Add(new RoadmapColor(color.Color, color.Name, color.Order, color.IsDefault));
        }

        return Result.Success();
    }

    /// <summary>
    /// Adds a manager to the Roadmap.
    /// </summary>
    /// <param name="roadmapManagerId"></param>
    /// <returns></returns>
    public Result AddManager(Guid roadmapManagerId, Guid currentUserEmployeeId)
    {
        // bypass manager check if no managers exist on initial creation
        if (!_objectConstruction)
        {
            var isManagerResult = CanModify(currentUserEmployeeId);
            if (isManagerResult.IsFailure)
            {
                return isManagerResult;
            }
        }

        if (_roadmapManagers.Any(x => x.ManagerId == roadmapManagerId))
        {
            return Result.Failure("Roadmap manager already exists on this roadmap.");
        }

        _roadmapManagers.Add(new RoadmapManager(this, roadmapManagerId));
        return Result.Success();
    }

    /// <summary>
    /// Removes a roadmap manager from the Roadmap.
    /// </summary>
    /// <param name="roadmapManagerId"></param>
    /// <returns></returns>
    public Result RemoveManager(Guid roadmapManagerId, Guid currentUserEmployeeId)
    {
        var isManagerResult = CanModify(currentUserEmployeeId);
        if (isManagerResult.IsFailure)
        {
            return isManagerResult;
        }

        var manager = _roadmapManagers.FirstOrDefault(x => x.ManagerId == roadmapManagerId);
        if (manager is null)
        {
            return Result.Failure("Roadmap manager does not exist on this roadmap.");
        }

        if (_roadmapManagers.Count == 1)
        {
            return Result.Failure("Roadmap must have at least one roadmap manager.");
        }

        _roadmapManagers.Remove(manager);
        return Result.Success();
    }


    /// <summary>
    /// Removes a root Roadmap Item from the Roadmap.
    /// </summary>
    /// <param name="childId"></param>
    /// <param name="currentUserEmployeeId"></param>
    /// <returns></returns>
    public Result RemoveChild(Guid childId, Guid currentUserEmployeeId)
    {
        var isManagerResult = CanModify(currentUserEmployeeId);
        if (isManagerResult.IsFailure)
        {
            return isManagerResult;
        }

        var child = _items.FirstOrDefault(x => x.Id == childId);
        if (child is null)
        {
            return Result.Failure("Child Roadmap Item does not exist on this roadmap.");
        }

        _items.Remove(child);

        ResetRootActivitiesOrder();

        return Result.Success();
    }

    /// <summary>
    /// Sets the order of the root child Roadmap Items. This is used to set the order of all child roadmap items at once.
    /// </summary>
    /// <param name="childActivities"></param>
    /// <param name="currentUserEmployeeId"></param>
    /// <returns></returns>
    public Result SetChildrenOrder(Dictionary<Guid, int> childActivities, Guid currentUserEmployeeId)
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

        return Result.Success();
    }

    /// <summary>
    /// Updates the order of the Roadmap Activities based on a single Roadmap Activity changing its order.
    /// </summary>
    /// <param name="activityId"></param>
    /// <param name="order"></param>
    /// <param name="currentUserEmployeeId"></param>
    /// <returns></returns>
    public Result SetActivityOrder(Guid activityId, int order, Guid currentUserEmployeeId)
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
        else
        {
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
        }

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
    /// Can the Roadmap be deleted.
    /// </summary>
    /// <param name="currentUserEmployeeId"></param>
    /// <returns></returns>
    public Result CanDelete(Guid currentUserEmployeeId)
    {
        return CanModify(currentUserEmployeeId);
    }

    /// <summary>
    /// Archives the Roadmap. Only active roadmaps can be archived.
    /// </summary>
    /// <param name="currentUserEmployeeId"></param>
    /// <returns></returns>
    public Result Archive(Guid currentUserEmployeeId)
    {
        if (!IsManager(currentUserEmployeeId))
            return Result.Failure("User is not a roadmap manager of this roadmap.");

        if (State != RoadmapState.Active)
            return Result.Failure("Only active roadmaps can be archived.");

        State = RoadmapState.Archived;

        return Result.Success();
    }

    /// <summary>
    /// Activates an archived Roadmap.
    /// </summary>
    /// <param name="currentUserEmployeeId"></param>
    /// <returns></returns>
    public Result Activate(Guid currentUserEmployeeId)
    {
        if (!IsManager(currentUserEmployeeId))
            return Result.Failure("User is not a roadmap manager of this roadmap.");

        if (State != RoadmapState.Archived)
            return Result.Failure("Only archived roadmaps can be activated.");

        State = RoadmapState.Active;

        return Result.Success();
    }

    private bool IsManager(Guid employeeId) => _roadmapManagers.Any(x => x.ManagerId == employeeId);

    /// <summary>
    /// Can the employee manage the Roadmap.
    /// </summary>
    /// <param name="employeeId"></param>
    /// <returns></returns>
    private Result CanEmployeeManage(Guid employeeId)
    {
        return IsManager(employeeId)
            ? Result.Success()
            : Result.Failure("User is not a roadmap manager of this roadmap.");
    }

    /// <summary>
    /// Can the employee modify the Roadmap.  The Roadmap must be active and the employee must be a manager.
    /// </summary>
    /// <param name="employeeId"></param>
    /// <returns></returns>
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

    public Result<T> CreateRoadmapItem<T>(
        IUpsertRoadmapItem newItem,
        Guid currentUserEmployeeId,
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
            return item;
        }
        catch (Exception ex)
        {
            return Result.Failure<T>(ex.Message);
        }
    }

    public Result<RoadmapActivity> CreateActivity(IUpsertRoadmapActivity newActivity, Guid currentUserEmployeeId)
    {
        return CreateRoadmapItem<RoadmapActivity>(
            newActivity,
            currentUserEmployeeId,
            (roadmapId, item) => RoadmapActivity.CreateRoot(roadmapId, (IUpsertRoadmapActivity)item, RootActivities.Count() + 1),
            (parent, item) => parent.CreateChildActivity((IUpsertRoadmapActivity)item)
        );
    }

    public Result<RoadmapMilestone> CreateMilestone(IUpsertRoadmapMilestone newMilestone, Guid currentUserEmployeeId)
    {
        return CreateRoadmapItem<RoadmapMilestone>(
            newMilestone,
            currentUserEmployeeId,
            (roadmapId, item) => RoadmapMilestone.Create(roadmapId, null, (IUpsertRoadmapMilestone)item),
            (parent, item) => parent.CreateChildMilestone((IUpsertRoadmapMilestone)item)
        );
    }

    public Result<RoadmapTimebox> CreateTimebox(IUpsertRoadmapTimebox newTimebox, Guid currentUserEmployeeId)
    {
        return CreateRoadmapItem<RoadmapTimebox>(
            newTimebox,
            currentUserEmployeeId,
            (roadmapId, item) => RoadmapTimebox.Create(roadmapId, null, (IUpsertRoadmapTimebox)item),
            (parent, item) => parent.CreateChildTimebox((IUpsertRoadmapTimebox)item)
        );
    }

    public Result UpdateRoadmapItem<T>(
        Guid itemId,
        IUpsertRoadmapItem item,
        Guid currentUserEmployeeId,
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

            var parentChanged = item.ParentId != roadmapItem.ParentId;
            var updateRootActivitiesOrder = parentChanged && (item.ParentId is null || roadmapItem.ParentId is null);

            var updateResult = updateFunc(roadmapItem, item, parentActivity);
            if (updateResult.IsFailure || !parentChanged)
            {
                return updateResult;
            }

            if (updateRootActivitiesOrder && typeof(T) == typeof(RoadmapActivity))
            {
                ResetRootActivitiesOrder();
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public Result UpdateActivity(Guid itemId, IUpsertRoadmapActivity activity, Guid currentUserEmployeeId)
    {
        return UpdateRoadmapItem<RoadmapActivity>(
            itemId,
            activity,
            currentUserEmployeeId,
            (roadmapActivity, item, parent) => roadmapActivity.Update((IUpsertRoadmapActivity)item, parent)
        );
    }

    public Result UpdateMilestone(Guid itemId, IUpsertRoadmapMilestone milestone, Guid currentUserEmployeeId)
    {
        return UpdateRoadmapItem<RoadmapMilestone>(
            itemId,
            milestone,
            currentUserEmployeeId,
            (roadmapMilestone, item, parent) => roadmapMilestone.Update((IUpsertRoadmapMilestone)item, parent)
        );
    }

    public Result UpdateTimebox(Guid itemId, IUpsertRoadmapTimebox timebox, Guid currentUserEmployeeId)
    {
        return UpdateRoadmapItem<RoadmapTimebox>(
            itemId,
            timebox,
            currentUserEmployeeId,
            (roadmapTimebox, item, parent) => roadmapTimebox.Update((IUpsertRoadmapTimebox)item, parent)
        );
    }

    /// <summary>
    /// Updates the date(s) of a Roadmap Item. The type of date update is determined by the OneOf parameter.
    /// </summary>
    /// <param name="itemId"></param>
    /// <param name="dateUpdate"></param>
    /// <param name="currentUserEmployeeId"></param>
    /// <returns></returns>
    public Result UpdateRoadmapItemDates(
        Guid itemId,
        OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange> dateUpdate,
        Guid currentUserEmployeeId)
    {
        var isManagerResult = CanModify(currentUserEmployeeId);
        if (isManagerResult.IsFailure)
            return isManagerResult;

        var item = _items.FirstOrDefault(x => x.Id == itemId);
        if (item is null)
            return Result.Failure("Roadmap Item does not exist on this roadmap.");

        return dateUpdate.Match<Result>(
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
    }


    /// <summary>
    /// Moves an Activity to a new parent Activity and sets the new order.
    /// </summary>
    /// <param name="activityId"></param>
    /// <param name="newParentId"></param>
    /// <param name="newOrder"></param>
    /// <param name="currentUserEmployeeId"></param>
    /// <returns></returns>
    public Result MoveActivity(Guid activityId, Guid? newParentId, int newOrder, Guid currentUserEmployeeId)
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

            var setOrderResult = SetActivityOrder(activityId, newOrder, currentUserEmployeeId);
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

        return Result.Success();
    }

    public Result DeleteItem(Guid itemId, Guid currentUserEmployeeId)
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

        var updateRootActivitiesOrder = !roadmapItem.ParentId.HasValue && roadmapItem.Type == RoadmapItemType.Activity;
        if (roadmapItem.ParentId.HasValue)
        {
            var changeParentResult = roadmapItem.ChangeParent(null);
            if (changeParentResult.IsFailure)
            {
                return changeParentResult;
            }
        }

        if (roadmapItem is RoadmapActivity activity)
        {
            var itemIdsToRemove = activity.GetSelfAndDescendants();
            var itemsToRemove = _items.Where(x => itemIdsToRemove.Contains(x.Id)).ToList();
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

    /// <summary>
    /// Creates a new Roadmap.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="dateRange"></param>
    /// <param name="visibility"></param>
    /// <param name="roadmapManagerIds"></param>
    /// <param name="color"></param>
    /// <returns></returns>
    public static Result<Roadmap> Create(string name, string? description, LocalDateRange dateRange, Visibility visibility, IEnumerable<Guid> roadmapManagerIds)
    {
        try
        {
            return new Roadmap(name, description, dateRange, visibility, RoadmapState.Active, roadmapManagerIds);
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
    public Result<Roadmap> Copy(string name, IEnumerable<Guid> roadmapManagerIds, Visibility visibility)
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

            return newRoadmap;
        }
        catch (Exception ex)
        {
            return Result.Failure<Roadmap>(ex.Message);
        }
    }
}
