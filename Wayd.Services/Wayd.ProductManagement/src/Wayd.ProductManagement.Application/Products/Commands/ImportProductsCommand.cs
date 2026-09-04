using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Additively imports a batch of products, parents before children so a child's parent always exists
/// by the time it is created.
/// <para>
/// Rows identify each other by a file-local <see cref="ImportProductDto.Number"/> that is never
/// persisted. Names cannot serve as the key: a tree legitimately holds the same name under two
/// different parents, and keying on them would make such a batch unimportable. A parent reference
/// therefore names another row in this same file — a product already in the catalog cannot be a
/// parent here, because this import stands a catalog up rather than grafting onto one.
/// </para>
/// <para>
/// The batch is all-or-nothing: types, statuses and tags are resolved by name and row numbers by
/// exact match, and any number that is duplicated or any reference that is unresolved, inactive or
/// circular fails the whole import, so a mistyped reference can never quietly attach a product to the
/// wrong parent or label it with the wrong tag.
/// </para>
/// </summary>
public sealed record ImportProductsCommand : ICommand
{
    public ImportProductsCommand(IEnumerable<ImportProductDto> products)
    {
        Products = [.. products];
    }

    public List<ImportProductDto> Products { get; }
}

public sealed class ImportProductsCommandValidator : AbstractValidator<ImportProductsCommand>
{
    public ImportProductsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Products)
            .NotNull()
            .NotEmpty();

        RuleForEach(p => p.Products)
            .NotNull()
            .SetValidator(new ImportProductDtoValidator());
    }
}

public sealed class ImportProductsCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ILogger<ImportProductsCommandHandler> logger,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ImportProductsCommand>
{
    private const string AppRequestName = nameof(ImportProductsCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ImportProductsCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ImportProductsCommand request, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        // One import run is one actor: the events say "the import", not "this person edited every row
        // by hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        try
        {
            var duplicateNumbers = request.Products
                .GroupBy(p => Normalize(p.Number), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateNumbers.Count > 0)
                return Fail($"The following product numbers appear more than once in the import: {Quote(duplicateNumbers)}.");

            var rowsByNumber = request.Products.ToDictionary(
                p => Normalize(p.Number), p => p, StringComparer.OrdinalIgnoreCase);

            var unresolvedParents = request.Products
                .Where(p => HasParent(p) && !rowsByNumber.ContainsKey(Normalize(p.ParentNumber!)))
                .Select(p => Normalize(p.ParentNumber!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (unresolvedParents.Count > 0)
            {
                return Fail(
                    $"The following parent numbers match no row in the import: {Quote(unresolvedParents)}. "
                    + "A parent must be another row in the same file; a product already in the catalog cannot be named as one here.");
            }

            var orderedResult = OrderParentsFirst(request.Products, rowsByNumber);
            if (orderedResult.IsFailure)
                return Result.Failure(orderedResult.Error);

            var typesResult = await ResolveProductTypes(request, cancellationToken);
            if (typesResult.IsFailure)
                return Result.Failure(typesResult.Error);
            var typesByName = typesResult.Value;

            var statusesResult = await ResolveStatuses(request, cancellationToken);
            if (statusesResult.IsFailure)
                return Result.Failure(statusesResult.Error);
            var (initialStatus, statusesByName) = statusesResult.Value;

            var tagsResult = await ResolveTags(request, cancellationToken);
            if (tagsResult.IsFailure)
                return Result.Failure(tagsResult.Error);
            var tagsByReference = tagsResult.Value;

            // Populated as rows are created so a child can look its parent up. This is the whole
            // reason the batch is ordered parents-first.
            Dictionary<string, Product> createdByNumber = new(StringComparer.OrdinalIgnoreCase);

            foreach (var row in orderedResult.Value)
            {
                var productType = typesByName[Normalize(row.ProductTypeName)];

                Guid? parentId = HasParent(row)
                    ? createdByNumber[Normalize(row.ParentNumber!)].Id
                    : null;

                var status = string.IsNullOrWhiteSpace(row.Status)
                    ? initialStatus
                    : statusesByName[Normalize(row.Status)];

                var product = Product.Create(
                    Normalize(row.Name),
                    row.Description,
                    productType.Id,
                    parentId,
                    row.ExternalId,
                    status,
                    actor,
                    timestamp);

                foreach (var reference in row.Tags)
                {
                    var (tag, category) = tagsByReference[Key(reference)];

                    var tagResult = product.Tag(tag, category, actor, timestamp);
                    if (tagResult.IsFailure)
                    {
                        return Fail(
                            $"Could not tag product '{row.Name}' with '{reference.CategoryName}|{reference.TagName}': {tagResult.Error}");
                    }
                }

                await _productManagementDbContext.Products.AddAsync(product, cancellationToken);

                createdByNumber.Add(Normalize(row.Number), product);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{AppRequestName}: imported {Count} product(s).", AppRequestName, request.Products.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {AppRequestName}", AppRequestName);

            return Result.Failure($"Exception for request {AppRequestName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Sorts the batch so every row follows its parent, and fails on a cycle.
    /// </summary>
    /// <remarks>
    /// A file lists rows in whatever order someone wrote them, so a child may well appear above its
    /// parent. Creating in file order would leave that child pointing at a parent that does not exist
    /// yet, which the domain cannot catch — it is handed a parent id and never queries the tree.
    /// </remarks>
    private Result<List<ImportProductDto>> OrderParentsFirst(
        IReadOnlyCollection<ImportProductDto> rows,
        Dictionary<string, ImportProductDto> rowsByNumber)
    {
        List<ImportProductDto> ordered = new(rows.Count);
        HashSet<string> placed = new(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            // Walks up to the root collecting anything not yet placed, then adds that chain
            // outermost-first. A row already placed by an earlier walk ends this one.
            List<ImportProductDto> chain = [];
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            var current = row;
            while (true)
            {
                var number = Normalize(current.Number);

                if (placed.Contains(number))
                    break;

                if (!seen.Add(number))
                {
                    return Fail<List<ImportProductDto>>(
                        $"The import contains a circular parent reference involving product '{number}'.");
                }

                chain.Add(current);

                if (!HasParent(current))
                    break;

                current = rowsByNumber[Normalize(current.ParentNumber!)];
            }

            for (var i = chain.Count - 1; i >= 0; i--)
            {
                ordered.Add(chain[i]);
                placed.Add(Normalize(chain[i].Number));
            }
        }

        return Result.Success(ordered);
    }

    /// <summary>
    /// Resolves product types by name. Type names carry a unique index, so no ambiguity check is
    /// needed — only an inactive type is reported, since one cannot be assigned to a new product.
    /// </summary>
    private async Task<Result<Dictionary<string, ProductType>>> ResolveProductTypes(
        ImportProductsCommand request, CancellationToken cancellationToken)
    {
        var typeNames = request.Products
            .Select(p => Normalize(p.ProductTypeName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var types = await _productManagementDbContext.ProductTypes
            .Where(t => typeNames.Contains(t.Name))
            .ToListAsync(cancellationToken);

        var inactive = types.Where(t => !t.IsActive).Select(t => t.Name).ToList();
        if (inactive.Count > 0)
        {
            return Fail<Dictionary<string, ProductType>>(
                $"The following product types are inactive and cannot be assigned: {Quote(inactive)}.");
        }

        var typesByName = types.ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

        var unresolved = typeNames.Where(n => !typesByName.ContainsKey(n)).ToList();
        if (unresolved.Count > 0)
            return Fail<Dictionary<string, ProductType>>($"Could not resolve the following product types: {Quote(unresolved)}.");

        return Result.Success(typesByName);
    }

    /// <summary>
    /// Loads the product workflow once, returning the initial status for rows naming none and a
    /// lookup for those that do.
    /// </summary>
    /// <remarks>
    /// Resolved through the governing workflow rather than by loading statuses directly, for the same
    /// reason <c>ChangeProductStatusCommand</c> does: that is what stops a row naming a status
    /// belonging to some other workflow.
    /// </remarks>
    private async Task<Result<(StatusRef Initial, Dictionary<string, StatusRef> ByName)>> ResolveStatuses(
        ImportProductsCommand request, CancellationToken cancellationToken)
    {
        // Product Management assigns workflows organization-wide, so the scope is null.
        var workflow = await _statusResolver.ForScope(
            ProductWorkflowOwners.Product.Key, scopeId: null, cancellationToken);

        if (workflow.IsFailure)
        {
            _logger.LogError("Unable to resolve the product workflow. Error message: {Error}", workflow.Error);
            return Result.Failure<(StatusRef, Dictionary<string, StatusRef>)>(workflow.Error);
        }

        var initial = await _statusResolver.Initial(
            ProductWorkflowOwners.Product.Key, scopeId: null, cancellationToken);

        if (initial.IsFailure)
        {
            _logger.LogError("Unable to resolve the initial product status. Error message: {Error}", initial.Error);
            return Result.Failure<(StatusRef, Dictionary<string, StatusRef>)>(initial.Error);
        }

        var statusesByName = workflow.Value.Statuses.ToDictionary(
            s => s.Name, StatusRef.From, StringComparer.OrdinalIgnoreCase);

        var named = request.Products
            .Where(p => !string.IsNullOrWhiteSpace(p.Status))
            .Select(p => Normalize(p.Status!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unresolved = named.Where(n => !statusesByName.ContainsKey(n)).ToList();
        if (unresolved.Count > 0)
        {
            return Fail<(StatusRef, Dictionary<string, StatusRef>)>(
                $"The following statuses do not belong to '{workflow.Value.Name}': {Quote(unresolved)}.");
        }

        return Result.Success((initial.Value, statusesByName));
    }

    /// <summary>
    /// Resolves every <c>Category|Tag</c> pair the batch names, keyed by both halves.
    /// </summary>
    /// <remarks>
    /// Both halves are needed because a tag name is unique only within its axis — two axes may each
    /// hold a <c>gold</c>, and resolving on the tag alone would pick whichever came back first.
    /// <para>
    /// Categories are loaded with their tags rather than querying tags directly: the aggregate needs
    /// the category anyway, since <see cref="ProductTagCategory.AllowsMany"/> decides whether a second
    /// tag on one axis joins the first or replaces it.
    /// </para>
    /// </remarks>
    private async Task<Result<Dictionary<string, (ProductTag Tag, ProductTagCategory Category)>>> ResolveTags(
        ImportProductsCommand request, CancellationToken cancellationToken)
    {
        var references = request.Products
            .SelectMany(p => p.Tags)
            .DistinctBy(Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (references.Count == 0)
            return Result.Success(new Dictionary<string, (ProductTag, ProductTagCategory)>(StringComparer.OrdinalIgnoreCase));

        var categoryNames = references
            .Select(r => Normalize(r.CategoryName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var categories = await _productManagementDbContext.ProductTagCategories
            .Include(c => c.Tags)
            .Where(c => categoryNames.Contains(c.Name))
            .ToListAsync(cancellationToken);

        var categoriesByName = categories.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

        var unresolvedCategories = categoryNames.Where(n => !categoriesByName.ContainsKey(n)).ToList();
        if (unresolvedCategories.Count > 0)
            return Fail<Dictionary<string, (ProductTag, ProductTagCategory)>>(
                $"Could not resolve the following tag categories: {Quote(unresolvedCategories)}.");

        var inactiveCategories = categories.Where(c => !c.IsActive).Select(c => c.Name).ToList();
        if (inactiveCategories.Count > 0)
            return Fail<Dictionary<string, (ProductTag, ProductTagCategory)>>(
                $"The following tag categories are inactive and cannot be used: {Quote(inactiveCategories)}.");

        Dictionary<string, (ProductTag, ProductTagCategory)> resolved = new(StringComparer.OrdinalIgnoreCase);
        List<string> unresolvedTags = [];
        List<string> inactiveTags = [];

        foreach (var reference in references)
        {
            var category = categoriesByName[Normalize(reference.CategoryName)];

            var tag = category.Tags.FirstOrDefault(
                t => string.Equals(t.Name, Normalize(reference.TagName), StringComparison.OrdinalIgnoreCase));

            if (tag is null)
            {
                unresolvedTags.Add(Key(reference));
                continue;
            }

            if (!tag.IsActive)
            {
                inactiveTags.Add(Key(reference));
                continue;
            }

            resolved.Add(Key(reference), (tag, category));
        }

        if (unresolvedTags.Count > 0)
            return Fail<Dictionary<string, (ProductTag, ProductTagCategory)>>(
                $"Could not resolve the following tags: {Quote(unresolvedTags)}.");

        if (inactiveTags.Count > 0)
            return Fail<Dictionary<string, (ProductTag, ProductTagCategory)>>(
                $"The following tags are inactive and cannot be applied: {Quote(inactiveTags)}.");

        return Result.Success(resolved);
    }

    /// <summary>The lookup key for a tag reference: both halves, since neither is unique alone.</summary>
    private static string Key(ProductTagReference reference) =>
        $"{Normalize(reference.CategoryName)}|{Normalize(reference.TagName)}";

    private static bool HasParent(ImportProductDto row) => !string.IsNullOrWhiteSpace(row.ParentNumber);

    private Result Fail(string message)
    {
        _logger.LogWarning("{AppRequestName}: {Message}", AppRequestName, message);
        return Result.Failure(message);
    }

    private Result<T> Fail<T>(string message)
    {
        _logger.LogWarning("{AppRequestName}: {Message}", AppRequestName, message);
        return Result.Failure<T>(message);
    }

    private static string Normalize(string value) => value.Trim();

    private static string Quote(IEnumerable<string> values) => string.Join(", ", values.Select(v => $"'{v}'"));
}
