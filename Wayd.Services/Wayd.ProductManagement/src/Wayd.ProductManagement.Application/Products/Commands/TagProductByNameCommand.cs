using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using Wayd.Common.Domain.Tagging;
using Wayd.ProductManagement.Application.ProductTags;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Products.Commands;

/// <summary>
/// Tags a product with a name someone typed: the tag on an axis with that name when there is exactly one, otherwise
/// the free tag with that name, created when none has it yet. Returns the id of the tag applied.
/// </summary>
/// <remarks>
/// Preferring a tag on an axis keeps "ios" typed by hand from becoming a free tag beside the curated one. A free
/// tag needs <see cref="ProductTagSettings.AllowsFreeTags"/>; a tag on an axis does not.
/// </remarks>
public sealed record TagProductByNameCommand(Guid Id, string Name) : ICommand<Guid>;

public sealed class TagProductByNameCommandValidator : AbstractValidator<TagProductByNameCommand>
{
    public TagProductByNameCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(TagBase.NameMaxLength);
    }
}

public sealed class TagProductByNameCommandHandler(
    IProductManagementDbContext productManagementDbContext,
    ISettings<ProductTagSettings> settings,
    ICurrentUser currentUser,
    ICurrentPrincipal currentPrincipal,
    ILogger<TagProductByNameCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<TagProductByNameCommand, Guid>
{
    private const string AppRequestName = nameof(TagProductByNameCommand);

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ISettings<ProductTagSettings> _settings = settings;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<TagProductByNameCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<Guid>> Handle(TagProductByNameCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var name = TagBase.ValidateFreeName(request.Name);
            if (name.IsFailure)
                return Result.Failure<Guid>(name.Error);

            var product = await _productManagementDbContext.Products
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
            if (product is null)
            {
                _logger.LogInformation("Product {ProductId} not found.", request.Id);
                return Result.Failure<Guid>("Product not found.");
            }

            var actor = EventActor.User(_currentUser.GetUserId(), await _currentPrincipal.GetEmployeeId(cancellationToken));
            var now = _dateTimeProvider.Now;

            var lowered = name.Value.ToLower();
            var onAxes = await _productManagementDbContext.ProductTags
                .Where(t => t.CategoryId != null && t.IsActive && t.Name.ToLower() == lowered)
                .Join(_productManagementDbContext.ProductTagCategories.Where(c => c.IsActive),
                    t => t.CategoryId, c => (Guid?)c.Id, (t, c) => new { Tag = t, Category = c })
                .ToListAsync(cancellationToken);

            if (onAxes.Count > 1)
                return Result.Failure<Guid>(ProductTagErrors.AmbiguousName);

            ProductTag tag;
            Result tagResult;
            if (onAxes.Count == 1)
            {
                tag = onAxes[0].Tag;
                tagResult = product.Tag(tag, onAxes[0].Category, actor, now);
            }
            else
            {
                if (!(await _settings.Get(cancellationToken)).AllowsFreeTags)
                    return Result.Failure<Guid>(ProductTagErrors.FreeTagsTurnedOff);

                var found = await FreeProductTags.FindOrCreate(_productManagementDbContext, name.Value, actor, now, cancellationToken);
                if (found.IsFailure)
                    return Result.Failure<Guid>(found.Error);

                tag = found.Value;
                tagResult = product.Tag(tag, null, actor, now);
            }

            if (tagResult.IsFailure)
                return Result.Failure<Guid>(tagResult.Error);

            try
            {
                await _productManagementDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // The product was given the tag, or the free tag was deleted for being unused, since the lookups.
                return Result.Failure<Guid>(ProductTagErrors.ChangedWhileSaving);
            }

            _logger.LogInformation("Product {ProductId} tagged with {TagId}.", request.Id, tag.Id);

            return tag.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
