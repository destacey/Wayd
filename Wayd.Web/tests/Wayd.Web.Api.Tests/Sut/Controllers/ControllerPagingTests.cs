using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Wayd.Common.Application.Models;
using Wayd.Web.Api.Controllers.Work;

namespace Wayd.Web.Api.Tests.Sut.Controllers;

/// <summary>
/// Holds every endpoint to one of the two paging shapes in <c>docs/contributing/api.mdx</c> (Paging): an action
/// answering with a <see cref="PagedResponse{T}"/> takes <c>page</c> and <c>pageSize</c>, one answering with a
/// <see cref="CursorPage{T}"/> takes <c>before</c> and <c>pageSize</c>, and nothing else pages.
/// </summary>
/// <remarks>
/// The hosted MCP server and the generated client both read an endpoint's paging from these parameter names, so
/// a third spelling is a place where callers page wrongly. The query side of the rule is held in
/// <c>Wayd.ArchitectureTests</c> (<c>PagingTests</c>).
/// </remarks>
public sealed class ControllerPagingTests
{
    /// <summary>Parameter names that mean paging, matched ignoring case as the query-string binder does.</summary>
    private static readonly HashSet<string> PagingParameterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "page", "pageNumber", "pageSize", "skip", "offset", "take", "before",
    };

    /// <summary>Property names that mark a response type as a page.</summary>
    private static readonly HashSet<string> PagingPropertyNames = new(StringComparer.Ordinal)
    {
        "PageNumber", "PageSize", "TotalPages", "HasNextPage", "NextCursor",
    };

    /// <summary>
    /// Actions that page in shapes of their own, each named with the issue that moves it to a shared shape.
    /// An entry is removed with the action; the list never grows.
    /// </summary>
    private static readonly Dictionary<string, string> ActionStrays = new()
    {
        ["BackgroundJobsController.GetJobs"] = "#1144",
        ["DeliveryOverviewController.GetRecentDeliveryEvents"] = "#1145",
        ["ImportsController.GetList"] = "#1144",
        ["ImportsController.GetRows"] = "#1144",
        ["MessagingController.GetDeadLetters"] = "#1144",
    };

    /// <summary>
    /// Response types that carry paging of their own, each named with the issue that removes it. An entry is
    /// removed with the type; the list never grows.
    /// </summary>
    private static readonly Dictionary<string, string> ResponseTypeStrays = new()
    {
        ["ImportProcessPageDto"] = "#1144",
        ["ImportProcessRowPageDto"] = "#1144",
        ["JobsResponse"] = "#1144",
        ["DeadLetterMessagesResponse"] = "#1144",
    };

    private sealed record Action(
        string Name,
        IReadOnlyList<string> Parameters,
        IReadOnlyDictionary<string, object?> Defaults,
        IReadOnlyList<Type> Responses);

    private static List<Action> Actions() =>
        typeof(WorkTypeLevelsController).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
                .Select(m => new Action($"{controller.Name}.{m.Name}", ParameterNames(m), Defaults(m), ResponseTypes(m))))
            .ToList();

    /// <summary>
    /// The names a caller sends: each parameter's, and for a parameter bound from the query string as an object,
    /// each of its properties'.
    /// </summary>
    private static List<string> ParameterNames(MethodInfo action) =>
        action.GetParameters()
            .Where(p => p.ParameterType != typeof(CancellationToken))
            .SelectMany(p => p.GetCustomAttribute<FromQueryAttribute>() is not null && IsObject(p.ParameterType)
                ? p.ParameterType.GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(x => x.Name)
                : [p.GetCustomAttribute<FromQueryAttribute>()?.Name ?? p.Name!])
            .ToList();

    /// <summary>Each parameter's default, by the name a caller sends; null for a parameter with none.</summary>
    private static Dictionary<string, object?> Defaults(MethodInfo action) =>
        action.GetParameters()
            .Where(p => p.ParameterType != typeof(CancellationToken))
            .ToDictionary(p => p.GetCustomAttribute<FromQueryAttribute>()?.Name ?? p.Name!, p => p.HasDefaultValue ? p.DefaultValue : null);

    private static bool IsObject(Type type) =>
        type.IsClass && type != typeof(string) && !type.IsArray && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type);

    /// <summary>
    /// What an action answers with on success: its declared result, seen through <see cref="Task{T}"/> and
    /// <see cref="ActionResult{T}"/>, or the 200 types it declares with <see cref="ProducesResponseTypeAttribute"/>.
    /// </summary>
    private static List<Type> ResponseTypes(MethodInfo action)
    {
        var type = action.ReturnType;
        if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>)))
        {
            type = type.GetGenericArguments()[0];
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ActionResult<>))
        {
            return [type.GetGenericArguments()[0]];
        }

        return action.GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Where(a => a.StatusCode == StatusCodes.Status200OK && a.Type is not null && a.Type != typeof(void))
            .Select(a => a.Type)
            .Distinct()
            .ToList();
    }

    /// <summary>The type and the types it is built from: generic arguments and array elements, to any depth.</summary>
    private static IEnumerable<Type> Constituents(Type type)
    {
        yield return type;
        var parts = type.IsArray ? [type.GetElementType()!] : type.IsGenericType ? type.GetGenericArguments() : [];
        foreach (var part in parts.SelectMany(Constituents))
        {
            yield return part;
        }
    }

    private static bool Answers(Action action, Type pageDefinition) =>
        action.Responses.Any(r => r.IsGenericType && r.GetGenericTypeDefinition() == pageDefinition);

    private static bool Takes(Action action, string name) => action.Parameters.Contains(name, StringComparer.Ordinal);

    /// <summary>Whether the action's <paramref name="name"/> parameter defaults to <paramref name="expected"/>.</summary>
    private static bool DefaultsTo(Action action, string name, object? expected) =>
        action.Defaults.TryGetValue(name, out var value) && Equals(value, expected);

    [Fact]
    public void ActionsAnsweringWithAPagedResponse_TakePageAndPageSize()
    {
        // Arrange
        var actions = Actions().Where(a => Answers(a, typeof(PagedResponse<>))).ToList();

        // Act
        var violations = actions
            .Where(a => !Takes(a, "page") || !Takes(a, "pageSize") || Takes(a, "before"))
            .Select(a => a.Name)
            .ToList();
        var ownDefaults = actions
            .Where(a => !DefaultsTo(a, "page", PageRequest.FirstPage) || !DefaultsTo(a, "pageSize", PageSizes.Default))
            .Select(a => a.Name)
            .ToList();

        // Assert — the action builds the PageRequest, so its signature is where the shared default must hold
        actions.Should().NotBeEmpty();
        violations.Should().BeEmpty("an action answering with PagedResponse<T> takes `page` and `pageSize`, and not `before`");
        ownDefaults.Should().BeEmpty("`page` defaults to PageRequest.FirstPage and `pageSize` to PageSizes.Default");
    }

    [Fact]
    public void ActionsAnsweringWithACursorPage_TakeBeforeAndPageSize()
    {
        // Arrange
        var actions = Actions().Where(a => Answers(a, typeof(CursorPage<>))).ToList();

        // Act
        var violations = actions
            .Where(a => !Takes(a, "before") || !Takes(a, "pageSize") || Takes(a, "page"))
            .Select(a => a.Name)
            .ToList();
        var ownDefaults = actions
            .Where(a => !DefaultsTo(a, "before", null) || !DefaultsTo(a, "pageSize", PageSizes.Default))
            .Select(a => a.Name)
            .ToList();

        // Assert — the action builds the CursorRequest, so its signature is where the shared default must hold
        actions.Should().NotBeEmpty();
        violations.Should().BeEmpty("an action answering with CursorPage<T> takes `before` and `pageSize`, and not `page`");
        ownDefaults.Should().BeEmpty("`before` defaults to null, for the first page, and `pageSize` to PageSizes.Default");
    }

    [Fact]
    public void ActionsAnsweringWithoutAPage_TakeNoPagingParameters()
    {
        // Arrange
        var actions = Actions()
            .Where(a => !Answers(a, typeof(PagedResponse<>)) && !Answers(a, typeof(CursorPage<>)))
            .ToList();

        // Act
        var violations = actions
            .Where(a => a.Parameters.Any(PagingParameterNames.Contains))
            .Select(a => a.Name)
            .ToList();

        // Assert — a list that pages answers with one of the two shapes; a whole list takes no paging
        violations.Except(ActionStrays.Keys).Should().BeEmpty(
            "only an action answering with PagedResponse<T> or CursorPage<T> takes paging parameters");
        ActionStrays.Keys.Except(violations).Should().BeEmpty(
            "an allow-listed action that now follows the rule, or no longer exists, comes off the list");
    }

    [Fact]
    public void ResponseTypes_CarryNoPagingButTheTwoShapes()
    {
        // Arrange
        var responses = Actions()
            .SelectMany(a => a.Responses)
            .SelectMany(Constituents)
            .Distinct()
            .ToList();

        // Act
        var violations = responses
            .Where(t => !t.IsGenericType || (t.GetGenericTypeDefinition() != typeof(PagedResponse<>) && t.GetGenericTypeDefinition() != typeof(CursorPage<>)))
            .Where(t => t.Name.EndsWith("Page", StringComparison.Ordinal)
                || t.Name.EndsWith("PageDto", StringComparison.Ordinal)
                || t.GetProperties().Any(p => PagingPropertyNames.Contains(p.Name)))
            .Select(t => t.Name)
            .Distinct()
            .ToList();

        // Assert
        violations.Except(ResponseTypeStrays.Keys).Should().BeEmpty(
            "a page of results is a PagedResponse<T> or a CursorPage<T>, never a type of its own");
        ResponseTypeStrays.Keys.Except(violations).Should().BeEmpty(
            "an allow-listed type that is no longer returned comes off the list");
    }
}
