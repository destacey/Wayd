using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.Versions.Dtos;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Versions.Commands;

/// <summary>
/// Additively imports a batch of versions, each planned against its product and then walked to the
/// state its dates describe by replaying the real lifecycle transitions.
/// <para>
/// There is no status column. A version's status follows from what happened to it: a row with no dates
/// is planned, a cut date makes it ready, and a released date makes it released. Replaying the
/// transitions rather than assigning a status is what gives an imported version the same status
/// history a hand-entered one would have.
/// </para>
/// <para>
/// Unlike a PPM program, a version needs no finalize pass. Nothing about a released version has to
/// exist first — <c>Version.MarkReleased</c> has no precondition that the version was ever cut, which
/// the domain documents as the case historical import depends on.
/// </para>
/// <para>
/// The batch is all-or-nothing: products are resolved by name, and any name that is duplicated within
/// the file, unresolved, ambiguous, or not releasable fails the whole import.
/// </para>
/// </summary>
public sealed record ImportVersionsCommand : ICommand
{
    public ImportVersionsCommand(IEnumerable<ImportVersionDto> versions)
    {
        Versions = [.. versions];
    }

    public List<ImportVersionDto> Versions { get; }
}

public sealed class ImportVersionsCommandValidator : AbstractValidator<ImportVersionsCommand>
{
    public ImportVersionsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(v => v.Versions)
            .NotNull()
            .NotEmpty();

        RuleForEach(v => v.Versions)
            .NotNull()
            .SetValidator(new ImportVersionDtoValidator());
    }
}

public sealed class ImportVersionsCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver,
    ICurrentUser currentUser,
    ILogger<ImportVersionsCommandHandler> logger,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ImportVersionsCommand>
{
    private const string AppRequestName = nameof(ImportVersionsCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ImportVersionsCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ImportVersionsCommand request, CancellationToken cancellationToken)
    {
        var timestamp = _dateTimeProvider.Now;

        // One import run is one actor: the events say "the import", not "this person recorded every
        // shipment by hand", while still recording who set it running.
        var actor = EventActor.Import(_currentUser.GetUserId());

        try
        {
            // A product may legitimately hold many versions, so only an exact (product, number) pair
            // repeating is a duplicate.
            var duplicates = request.Versions
                .GroupBy(v => Key(v.ProductName, v.Number), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicates.Count > 0)
                return Fail($"The following versions appear more than once in the import: {Quote(duplicates)}.");

            var productsResult = await ResolveProducts(request, cancellationToken);
            if (productsResult.IsFailure)
                return Result.Failure(productsResult.Error);
            var productsByName = productsResult.Value;

            var existingResult = await FindExisting(request, productsByName, cancellationToken);
            if (existingResult.IsFailure)
                return Result.Failure(existingResult.Error);

            var statusesResult = await ResolveStatuses(cancellationToken);
            if (statusesResult.IsFailure)
                return Result.Failure(statusesResult.Error);
            var statuses = statusesResult.Value;

            foreach (var row in request.Versions)
            {
                var product = productsByName[Normalize(row.ProductName)];

                var createResult = Version.Create(
                    product.Id,
                    Normalize(row.Number),
                    row.Name,
                    row.TargetDate,
                    row.Sequence,
                    product.IsReleasable,
                    statuses.Initial,
                    product.Name,
                    actor,
                    timestamp);

                if (createResult.IsFailure)
                    return Fail($"Could not plan version '{row.Number}' for product '{row.ProductName}': {createResult.Error}");

                var version = createResult.Value;

                var walkResult = Walk(version, row, statuses, product.Name, actor, timestamp);
                if (walkResult.IsFailure)
                    return Result.Failure(walkResult.Error);

                await _productManagementDbContext.Versions.AddAsync(version, cancellationToken);
            }

            await _productManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("{AppRequestName}: imported {Count} version(s).", AppRequestName, request.Versions.Count);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for request {AppRequestName}", AppRequestName);

            return Result.Failure($"Exception for request {AppRequestName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Walks a freshly planned version to the state its dates describe, in the order the events
    /// actually happen.
    /// </summary>
    /// <remarks>
    /// Each step is the same domain method a person's click would reach, so the status history reads
    /// as though the version were recorded as it went rather than in one sitting. Notes are applied
    /// last: <c>UpdateDetails</c> is an edit rather than a lifecycle step, and a withdrawn or released
    /// version still accepts one.
    /// </remarks>
    private Result Walk(
        Version version,
        ImportVersionDto row,
        VersionStatuses statuses,
        string productName,
        EventActor actor,
        Instant timestamp)
    {
        if (row.CutDate is not null)
        {
            var cutResult = version.Cut(row.CutDate.Value, statuses.Ready, productName, actor, timestamp);
            if (cutResult.IsFailure)
                return Fail($"Could not cut version '{row.Number}' for product '{row.ProductName}': {cutResult.Error}");
        }

        if (row.ReleasedDate is not null)
        {
            var releaseResult = version.MarkReleased(row.ReleasedDate.Value, statuses.Released, productName, actor, timestamp);
            if (releaseResult.IsFailure)
                return Fail($"Could not release version '{row.Number}' for product '{row.ProductName}': {releaseResult.Error}");
        }

        if (!string.IsNullOrWhiteSpace(row.Notes))
        {
            var notesResult = version.UpdateDetails(
                Normalize(row.Number), row.Name, row.Notes, row.Sequence, actor, timestamp);
            if (notesResult.IsFailure)
                return Fail($"Could not set notes on version '{row.Number}' for product '{row.ProductName}': {notesResult.Error}");
        }

        return Result.Success();
    }

    /// <summary>
    /// Resolves the products the batch names, with the releasability their type decides.
    /// </summary>
    /// <remarks>
    /// Product names carry no unique index, so an ambiguous name fails the batch rather than guessing
    /// which product a version belongs to. Releasability is checked here as well as by the aggregate:
    /// reporting every offending product at once beats failing on the first row that happens to use
    /// one.
    /// </remarks>
    private async Task<Result<Dictionary<string, ResolvedProduct>>> ResolveProducts(
        ImportVersionsCommand request, CancellationToken cancellationToken)
    {
        var productNames = request.Versions
            .Select(v => Normalize(v.ProductName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var products = await _productManagementDbContext.Products
            .Where(p => productNames.Contains(p.Name))
            .Join(
                _productManagementDbContext.ProductTypes,
                p => p.ProductTypeId,
                t => t.Id,
                (p, t) => new ResolvedProduct(p.Id, p.Name, t.IsReleasable))
            .ToListAsync(cancellationToken);

        var ambiguous = products
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (ambiguous.Count > 0)
        {
            return Fail<Dictionary<string, ResolvedProduct>>(
                $"The following product names match more than one product: {Quote(ambiguous)}.");
        }

        var productsByName = products.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var unresolved = productNames.Where(n => !productsByName.ContainsKey(n)).ToList();
        if (unresolved.Count > 0)
            return Fail<Dictionary<string, ResolvedProduct>>($"Could not resolve the following products: {Quote(unresolved)}.");

        var notReleasable = products.Where(p => !p.IsReleasable).Select(p => p.Name).ToList();
        if (notReleasable.Count > 0)
        {
            return Fail<Dictionary<string, ResolvedProduct>>(
                $"Versions cannot be cut against the following products, whose type is not releasable: {Quote(notReleasable)}.");
        }

        return Result.Success(productsByName);
    }

    /// <summary>
    /// Fails the batch when a version it would create already exists.
    /// </summary>
    /// <remarks>
    /// Unlike products — where a repeated name is ordinary and no such check is possible — a product
    /// holding two versions with the same number is a mistake, so re-running a file is refused rather
    /// than silently duplicating shipments.
    /// </remarks>
    private async Task<Result> FindExisting(
        ImportVersionsCommand request,
        Dictionary<string, ResolvedProduct> productsByName,
        CancellationToken cancellationToken)
    {
        var productIds = productsByName.Values.Select(p => p.Id).ToList();

        var existing = await _productManagementDbContext.Versions
            .Where(v => productIds.Contains(v.ProductId))
            .Select(v => new { v.ProductId, v.Number })
            .ToListAsync(cancellationToken);

        if (existing.Count == 0)
            return Result.Success();

        var existingKeys = existing
            .Join(productsByName.Values, e => e.ProductId, p => p.Id, (e, p) => Key(p.Name, e.Number))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var clashes = request.Versions
            .Select(v => Key(v.ProductName, v.Number))
            .Where(existingKeys.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return clashes.Count > 0
            ? Fail($"The following versions already exist: {Quote(clashes)}.")
            : Result.Success();
    }

    /// <summary>
    /// Resolves the three statuses a version can reach during an import, once for the whole batch.
    /// </summary>
    private async Task<Result<VersionStatuses>> ResolveStatuses(CancellationToken cancellationToken)
    {
        // Product Management assigns workflows organization-wide, so the scope is null.
        var initial = await _statusResolver.Initial(ProductWorkflowOwners.Version.Key, scopeId: null, cancellationToken);
        if (initial.IsFailure)
        {
            _logger.LogError("Unable to resolve the initial version status. Error message: {Error}", initial.Error);
            return Result.Failure<VersionStatuses>(initial.Error);
        }

        var ready = await _statusResolver.ForAlias(
            ProductWorkflowOwners.Version.Key, scopeId: null, (int)ProductStatusAlias.Ready, cancellationToken);
        if (ready.IsFailure)
        {
            _logger.LogError("Unable to resolve the ready version status. Error message: {Error}", ready.Error);
            return Result.Failure<VersionStatuses>(ready.Error);
        }

        var released = await _statusResolver.ForAlias(
            ProductWorkflowOwners.Version.Key, scopeId: null, (int)ProductStatusAlias.Released, cancellationToken);
        if (released.IsFailure)
        {
            _logger.LogError("Unable to resolve the released version status. Error message: {Error}", released.Error);
            return Result.Failure<VersionStatuses>(released.Error);
        }

        return Result.Success(new VersionStatuses(initial.Value, ready.Value, released.Value));
    }

    /// <summary>A product the batch names, with what its type permits.</summary>
    private sealed record ResolvedProduct(Guid Id, string Name, bool IsReleasable);

    /// <summary>The statuses an imported version can pass through.</summary>
    private sealed record VersionStatuses(StatusRef Initial, StatusRef Ready, StatusRef Released);

    /// <summary>Identifies a version: the number alone is only unique within its product.</summary>
    private static string Key(string productName, string number) =>
        $"{Normalize(productName)} {Normalize(number)}";

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
