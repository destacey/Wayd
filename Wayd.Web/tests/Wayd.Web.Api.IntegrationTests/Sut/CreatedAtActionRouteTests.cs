using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Guards the route-name drift that only shows up on a real create call.
/// </summary>
/// <remarks>
/// <c>CreatedAtAction</c> resolves its target by name at runtime and throws
/// <c>InvalidOperationException("No route matches the supplied values.")</c> when the anonymous object
/// does not supply the target's route parameters. The record is already saved by then, so the caller
/// sees a 500 from an operation that in fact succeeded.
/// <para>
/// Nothing else catches it: the compiler cannot see inside the anonymous object, and the unit suite
/// never routes. Renaming a GET's route parameter — <c>{id}</c> to <c>{idOrKey}</c>, say — silently
/// breaks every POST that pointed at it.
/// </para>
/// <para>
/// Reflected over the assembly rather than exercised per endpoint, so a controller added later is
/// covered without anyone remembering to add a case.
/// </para>
/// </remarks>
public sealed class CreatedAtActionRouteTests
{
    /// <summary>Route parameters ASP.NET supplies itself, so a call site need not.</summary>
    private static readonly HashSet<string> AmbientRouteParameters =
        new(StringComparer.OrdinalIgnoreCase) { "controller", "action", "area", "version" };

    [Fact]
    public void EveryCreatedAtAction_SuppliesTheTargetRouteParameters()
    {
        // Arrange
        var controllers = typeof(Program).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(controllers);

        var failures = new List<string>();

        foreach (var controller in controllers)
        {
            var source = ControllerSource(controller);
            if (source is null)
            {
                continue;
            }

            foreach (var (targetAction, suppliedNames, snippet) in CreatedAtActionCalls(source))
            {
                var target = controller
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(m => m.Name == targetAction);

                if (target is null)
                {
                    // nameof() would not compile against a missing method, so this only happens when
                    // the target lives on another controller — which CreatedAtAction cannot reach.
                    failures.Add($"{controller.Name}: CreatedAtAction targets '{targetAction}', which is not an action on this controller. {snippet}");
                    continue;
                }

                var required = RouteTemplateParameters(target);
                var missing = required
                    .Where(p => !AmbientRouteParameters.Contains(p) && !suppliedNames.Contains(p, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                if (missing.Count > 0)
                {
                    failures.Add(
                        $"{controller.Name}.{targetAction} routes on {{{string.Join("}, {", required)}}} but the call supplies " +
                        $"[{string.Join(", ", suppliedNames)}] — missing [{string.Join(", ", missing)}]. " +
                        $"This returns 500 after the record is created. {snippet}");
                }
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>The route parameter names in an action's own template plus its controller's.</summary>
    private static IReadOnlyCollection<string> RouteTemplateParameters(MethodInfo action)
    {
        var templates = action.GetCustomAttributes<HttpMethodAttribute>()
            .Select(a => a.Template)
            .Concat(action.DeclaringType!.GetCustomAttributes<RouteAttribute>().Select(a => a.Template))
            .Where(t => !string.IsNullOrWhiteSpace(t));

        // {id}, {id:guid} and {id?} all name the parameter "id".
        return templates
            .SelectMany(t => Regex.Matches(t!, @"\{([^}:?*]+)").Select(m => m.Groups[1].Value.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Every <c>CreatedAtAction(nameof(X), new { ... }, ...)</c> in the file, with the property names
    /// the anonymous object supplies. A <c>null</c> route-values argument supplies nothing.
    /// </summary>
    private static IEnumerable<(string TargetAction, IReadOnlyCollection<string> Supplied, string Snippet)> CreatedAtActionCalls(
        string source)
    {
        foreach (Match call in Regex.Matches(
            source,
            @"CreatedAtAction\(\s*nameof\((?<action>\w+)\)\s*,\s*(?<values>null|new\s*\{(?<body>[^}]*)\})",
            RegexOptions.Singleline))
        {
            var body = call.Groups["body"].Success ? call.Groups["body"].Value : string.Empty;

            // "id = x" names id; a bare "id" names id via C#'s projection initializer shorthand.
            var supplied = body
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.Split('=', 2)[0].Trim())
                .Select(name => name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name)
                .Where(name => Regex.IsMatch(name, @"^\w+$"))
                .ToList();

            yield return (
                call.Groups["action"].Value,
                supplied,
                $"({Regex.Replace(call.Value, @"\s+", " ")}…)");
        }
    }

    /// <summary>The controller's source file, found by name under the repository's controller tree.</summary>
    private static string? ControllerSource(Type controller)
    {
        var root = RepositoryRoot();
        if (root is null)
        {
            return null;
        }

        var path = Directory
            .EnumerateFiles(Path.Combine(root, "Wayd.Web", "src", "Wayd.Web.Api", "Controllers"),
                $"{controller.Name}.cs", SearchOption.AllDirectories)
            .FirstOrDefault();

        return path is null ? null : File.ReadAllText(path);
    }

    private static string? RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wayd.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName;
    }
}
