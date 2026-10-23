using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Wayd.Web.Api.Controllers.Work;

namespace Wayd.Web.Api.Tests.Sut.Controllers;

public sealed class ControllerRouteTests
{
    /// <summary>
    /// Every action's route as the router sees it: an action template starting with "/" or "~/" replaces the
    /// controller's route rather than extending it.
    /// </summary>
    private static IEnumerable<(string Action, string Route)> ActionRoutes() =>
        typeof(WorkTypeLevelsController).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(controller =>
            {
                var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? "";
                return controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().Select(a => (Method: m, a.Template)))
                    .Select(x => ($"{controller.Name}.{x.Method.Name}", Combine(prefix, x.Template)));
            });

    private static string Combine(string prefix, string? template) => template switch
    {
        null or "" => "/" + prefix,
        _ when template.StartsWith("~/", StringComparison.Ordinal) => template[1..],
        _ when template.StartsWith('/') => template,
        _ => $"/{prefix}/{template}",
    };

    [Fact]
    public void EveryAction_IsRoutedUnderApi()
    {
        // Arrange
        var routes = ActionRoutes().ToList();

        // Act
        var outside = routes.Where(r => !r.Route.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)).ToList();

        // Assert — a leading "/" on an action's template drops the controller's route, putting it at the server root.
        routes.Should().NotBeEmpty();
        outside.Should().BeEmpty();
    }

    [Fact]
    public void WorkTypeLevelsUpdateOrder_IsRoutedUnderItsController()
    {
        // Arrange
        var routes = ActionRoutes();

        // Act
        var route = routes.Single(r => r.Action == $"{nameof(WorkTypeLevelsController)}.{nameof(WorkTypeLevelsController.UpdateOrder)}").Route;

        // Assert
        route.Should().Be("/api/work/work-type-levels/order");
    }
}
