using CSharpFunctionalExtensions;
using FluentAssertions;
using Wayd.ArchitectureTests.Helpers;
using Wayd.Common.Application.Models;

namespace Wayd.ArchitectureTests.Sut;

/// <summary>
/// Holds every paged query to the two shared paging shapes: a query answering with a
/// <see cref="PagedResponse{T}"/> takes a <see cref="PageRequest"/>, one answering with a
/// <see cref="CursorPage{T}"/> takes a <see cref="CursorRequest"/>, and each validates it with the shared
/// validator, so no list invents its own default or bounds.
/// </summary>
/// <remarks>
/// The controller side of the rule — which parameters an action takes — is held in
/// <c>Wayd.Web.Api.Tests</c> (<c>ControllerPagingTests</c>), the project that loads the API.
/// </remarks>
public sealed class PagingTests
{
    /// <summary>The names a query uses for paging when it does not take a shared request.</summary>
    private static readonly HashSet<string> LoosePagingNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Page", "PageNumber", "PageSize", "Skip", "Offset", "Take", "Before",
    };

    /// <summary>
    /// Queries that page in shapes of their own, each named with the issue that moves it to a shared shape.
    /// An entry is removed with the query; the list never grows.
    /// </summary>
    private static readonly Dictionary<string, string> LoosePagingStrays = new()
    {
        ["Wayd.Common.Application.Imports.Queries.GetImportProcessesQuery"] = "#1144",
        ["Wayd.Common.Application.Imports.Queries.GetImportProcessRowsQuery"] = "#1144",
        ["Wayd.ProductManagement.Application.DeliveryOverview.Queries.GetRecentDeliveryEventsQuery"] = "#1145",
    };

    /// <summary>
    /// Validators that allow pages larger than <see cref="PageSizes.Max"/>, each with the reason its list needs
    /// them. A larger page costs the database and the response as much whoever asks, so an entry is added only
    /// for a list whose callers need it, and its endpoint description states the maximum.
    /// </summary>
    private static readonly Dictionary<string, string> LargerPageValidators = new();

    private static List<Type> ApplicationTypes() =>
        AssemblyHelper.GetApplicationAssemblies()
            .SelectMany(a => a.GetTypes())
            .ToList();

    private static List<Type> Queries() =>
        ApplicationTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && QueryResponse(t) is not null)
            .ToList();

    /// <summary>The <c>TResponse</c> of the type's <c>IQuery&lt;TResponse&gt;</c>, or null when it is not a query.</summary>
    private static Type? QueryResponse(Type type) =>
        type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().Name == "IQuery`1")
            ?.GetGenericArguments()[0];

    /// <summary>The page type a query answers with, seen through <see cref="Result{T}"/>.</summary>
    private static Type? PageDefinition(Type response)
    {
        if (response.IsGenericType && response.GetGenericTypeDefinition() == typeof(Result<>))
        {
            response = response.GetGenericArguments()[0];
        }

        return response.IsGenericType ? response.GetGenericTypeDefinition() : null;
    }

    /// <summary>Whether a concrete validator derives from <paramref name="validatorBase"/> closed over the query.</summary>
    private static bool HasValidator(Type query, Type validatorBase, List<Type> types) =>
        types.Any(t => t is { IsClass: true, IsAbstract: false } && Ancestors(t).Any(b =>
            b.IsGenericType
            && b.GetGenericTypeDefinition() == validatorBase
            && b.GetGenericArguments()[0] == query));

    private static IEnumerable<Type> Ancestors(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }

    [Fact]
    public void QueriesAnsweringWithAPage_TakeTheSharedRequest()
    {
        // Arrange
        var queries = Queries();

        // Act
        var paged = queries
            .Select(q => (Query: q, Page: PageDefinition(QueryResponse(q)!)))
            .ToList();
        var violations = paged
            .Where(x => (x.Page == typeof(PagedResponse<>) && !typeof(IPagedQuery).IsAssignableFrom(x.Query))
                || (x.Page == typeof(CursorPage<>) && !typeof(ICursorQuery).IsAssignableFrom(x.Query)))
            .Select(x => x.Query.FullName)
            .ToList();

        // Assert — each shape is seen, so a response this rule no longer unwraps cannot leave it checking nothing
        paged.Select(x => x.Page).Should().Contain(typeof(PagedResponse<>)).And.Contain(typeof(CursorPage<>));
        violations.Should().BeEmpty(
            "a query answering with PagedResponse<T> implements IPagedQuery, and one answering with CursorPage<T> implements ICursorQuery. Violating types: {0}",
            string.Join(", ", violations));
    }

    [Fact]
    public void PagedQueries_AreValidatedByThePagedQueryValidator()
    {
        // Arrange
        var types = ApplicationTypes();
        var queries = types.Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPagedQuery).IsAssignableFrom(t)).ToList();

        // Act
        var unvalidated = queries
            .Where(q => !HasValidator(q, typeof(PagedQueryValidator<>), types))
            .Select(q => q.FullName)
            .ToList();

        // Assert
        queries.Should().NotBeEmpty();
        unvalidated.Should().BeEmpty(
            "every IPagedQuery has a validator deriving from PagedQueryValidator<TQuery>, which bounds its page. Violating types: {0}",
            string.Join(", ", unvalidated));
    }

    [Fact]
    public void CursorQueries_AreValidatedByTheCursorQueryValidator()
    {
        // Arrange
        var types = ApplicationTypes();
        var queries = types.Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ICursorQuery).IsAssignableFrom(t)).ToList();

        // Act
        var unvalidated = queries
            .Where(q => !HasValidator(q, typeof(CursorQueryValidator<,>), types))
            .Select(q => q.FullName)
            .ToList();

        // Assert
        queries.Should().NotBeEmpty();
        unvalidated.Should().BeEmpty(
            "every ICursorQuery has a validator deriving from CursorQueryValidator<TQuery, TCursor>, which bounds its page and reads its cursor. Violating types: {0}",
            string.Join(", ", unvalidated));
    }

    [Fact]
    public void Queries_DoNotCarryTheirOwnPagingParameters()
    {
        // Arrange
        var queries = Queries();

        // Act
        var violations = queries
            .Where(q => q.GetProperties().Any(p => LoosePagingNames.Contains(p.Name)))
            .Select(q => q.FullName!)
            .ToList();

        // Assert — paging travels in the shared request, so its default and bounds are the shared ones
        violations.Except(LoosePagingStrays.Keys).Should().BeEmpty(
            "a paged query takes a PageRequest or CursorRequest as its Paging property rather than paging properties of its own. Violating types: {0}",
            string.Join(", ", violations.Except(LoosePagingStrays.Keys)));
        LoosePagingStrays.Keys.Except(violations).Should().BeEmpty(
            "an allow-listed query that now follows the rule, or no longer exists, comes off the list");
    }

    [Fact]
    public void PagedQueryValidators_KeepTheSharedMaximum_UnlessAllowListed()
    {
        // Arrange
        var validators = ApplicationTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && Ancestors(t).Any(b => b.IsGenericType
                && (b.GetGenericTypeDefinition() == typeof(PagedQueryValidator<>) || b.GetGenericTypeDefinition() == typeof(CursorQueryValidator<,>))))
            .ToList();

        // Act
        var uncheckable = validators
            .Where(v => v.GetConstructor(Type.EmptyTypes) is null)
            .Select(v => v.FullName!)
            .ToList();
        var larger = validators
            .Where(v => v.GetConstructor(Type.EmptyTypes) is not null
                && (int)v.GetProperty("MaxPageSize")!.GetValue(Activator.CreateInstance(v))! != PageSizes.Max)
            .Select(v => v.FullName!)
            .ToList();

        // Assert
        validators.Should().NotBeEmpty();
        uncheckable.Should().BeEmpty(
            "a paging validator takes no constructor arguments, so this rule can read its maximum. Violating types: {0}",
            string.Join(", ", uncheckable));
        larger.Except(LargerPageValidators.Keys).Should().BeEmpty(
            "a list allows pages up to PageSizes.Max unless it is allow-listed with the reason it needs more. Violating types: {0}",
            string.Join(", ", larger.Except(LargerPageValidators.Keys)));
        LargerPageValidators.Keys.Except(larger).Should().BeEmpty(
            "an allow-listed validator that no longer allows larger pages, or no longer exists, comes off the list");
    }
}
