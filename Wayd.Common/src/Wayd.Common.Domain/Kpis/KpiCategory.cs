using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events.Kpis;
using Wayd.Common.Extensions;

namespace Wayd.Common.Domain.Kpis;

/// <summary>
/// A grouping the organization defines for KPIs — Flow, Quality, People — that a scorecard is grouped by, as a
/// Balanced Scorecard groups by perspective.
/// </summary>
/// <remarks>
/// Defined by each organization rather than fixed, since perspectives vary between companies. One list for every
/// subject type. A category KPIs use is deactivated rather than deleted: it stops being offered for new KPIs and keeps
/// grouping the ones that have it.
/// </remarks>
public sealed class KpiCategory : BaseAuditableEntity
{
    /// <summary>The longest name a category holds.</summary>
    public const int NameMaxLength = 64;

    /// <summary>The longest description a category holds.</summary>
    public const int DescriptionMaxLength = 512;

    private string _name = default!;
    private string? _description;

    private KpiCategory() { }

    private KpiCategory(string name, string? description, int order)
    {
        Name = name;
        Description = description;
        Order = order;
        IsActive = true;
    }

    /// <summary>
    /// Defines a category. Whether another category already has the name is the caller's to check.
    /// </summary>
    public static Result<KpiCategory> Create(string name, string? description, int order, EventActor actor, Instant timestamp)
    {
        var error = ValidateDetails(name, description);
        if (error is not null)
            return Result.Failure<KpiCategory>(error);

        var category = new KpiCategory(name, description, order);
        category.AddDomainEvent(new KpiCategoryCreatedEvent(category.Id, category.Name, category.Description, order, actor, timestamp));

        return category;
    }

    /// <summary>What the category is called. Unique across the organization, ignoring case.</summary>
    public string Name
    {
        get => _name;
        private set => _name = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    }

    /// <summary>What kind of KPI belongs in the category.</summary>
    public string? Description
    {
        get => _description;
        private set => _description = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>Its position in the list and on a scorecard, lowest first.</summary>
    public int Order { get; private set; }

    /// <summary>Whether it is offered for new KPIs.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Changes the name and description. Whether another category already has the name is the caller's to check.
    /// Raises nothing when nothing changed.
    /// </summary>
    public Result UpdateDetails(string name, string? description, EventActor actor, Instant timestamp)
    {
        var error = ValidateDetails(name, description);
        if (error is not null)
            return Result.Failure(error);

        var previousName = Name;
        var previousDescription = Description;

        Name = name;
        Description = description;

        if (Name != previousName || Description != previousDescription)
            AddDomainEvent(new KpiCategoryDetailsUpdatedEvent(Id, Name, Description, previousName, previousDescription, actor, timestamp));

        return Result.Success();
    }

    /// <summary>Offers the category for new KPIs again.</summary>
    public Result Activate(EventActor actor, Instant timestamp)
    {
        if (IsActive)
            return Result.Failure("This category is already active.");

        IsActive = true;
        AddDomainEvent(new KpiCategoryActivatedEvent(Id, actor, timestamp));

        return Result.Success();
    }

    /// <summary>Stops offering the category for new KPIs. KPIs that have it keep it.</summary>
    public Result Deactivate(EventActor actor, Instant timestamp)
    {
        if (!IsActive)
            return Result.Failure("This category is already inactive.");

        IsActive = false;
        AddDomainEvent(new KpiCategoryDeactivatedEvent(Id, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the category is being deleted. Whether any KPI has it is the caller's to check first; the caller
    /// then removes it.
    /// </summary>
    public void Delete(EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(actor, nameof(actor));
        AddDomainEvent(new KpiCategoryDeletedEvent(Id, Name, actor, timestamp));
    }

    /// <summary>
    /// Puts the categories in the given order, numbering them from zero. Each category whose position changed raises
    /// its own event.
    /// </summary>
    /// <param name="categories">Every category.</param>
    /// <param name="orderedIds">The same categories' ids, each exactly once, in their new order.</param>
    public static Result Reorder(IReadOnlyCollection<KpiCategory> categories, IReadOnlyList<Guid> orderedIds, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(categories, nameof(categories));
        Guard.Against.Null(orderedIds, nameof(orderedIds));

        var byId = categories.ToDictionary(c => c.Id);
        if (orderedIds.Count != byId.Count || orderedIds.Distinct().Count() != orderedIds.Count || !orderedIds.All(byId.ContainsKey))
            return Result.Failure("The new order must list every category exactly once.");

        for (var position = 0; position < orderedIds.Count; position++)
        {
            var category = byId[orderedIds[position]];
            if (category.Order == position)
                continue;

            var from = category.Order;
            category.Order = position;
            category.AddDomainEvent(new KpiCategoryReorderedEvent(category.Id, from, position, actor, timestamp));
        }

        return Result.Success();
    }

    private static string? ValidateDetails(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "A category needs a name.";
        if (name.Trim().Length > NameMaxLength)
            return $"A category name can be at most {NameMaxLength} characters.";
        if (description?.Trim().Length > DescriptionMaxLength)
            return $"A category description can be at most {DescriptionMaxLength} characters.";

        return null;
    }
}
