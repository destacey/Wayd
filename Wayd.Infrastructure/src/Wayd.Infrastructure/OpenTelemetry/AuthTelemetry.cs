using System.Diagnostics;

namespace Wayd.Infrastructure.OpenTelemetry;

/// <summary>
/// Names for the span a permission check records under the current request. ASP.NET Core emits a counter for
/// authorization and no activity, so a check is otherwise indistinguishable from the queries that follow it.
/// </summary>
public static class AuthTelemetry
{
    /// <summary>The activity source the tracer provider must subscribe to. An unregistered source drops every check.</summary>
    public const string Name = "Wayd.Auth";

    /// <summary>The span name for one permission check.</summary>
    public const string Check = "authz.check";

    /// <summary>The authorization policy, or the comma-separated permissions when the policy accepts any of them.</summary>
    public const string PolicyTag = "aspnetcore.authorization.policy";

    /// <summary>Whether the check allowed the caller. <see cref="Success"/> or <see cref="Failure"/>.</summary>
    public const string ResultTag = "aspnetcore.authorization.result";

    /// <summary>The result tag value when the check allowed the caller.</summary>
    public const string Success = "success";

    /// <summary>The result tag value when the check denied the caller.</summary>
    public const string Failure = "failure";

    /// <summary>The source permission checks start activities on.</summary>
    public static readonly ActivitySource Source = new(Name);

    /// <summary>Starts a permission-check span and tags it with the policy being evaluated.</summary>
    public static Activity? StartCheck(string policy)
    {
        var activity = Source.StartActivity(Check);
        activity?.SetTag(PolicyTag, policy);
        return activity;
    }

    /// <summary>Records whether the check allowed the caller. The semantic convention values are success and failure.</summary>
    public static void SetResult(Activity? activity, bool allowed) =>
        activity?.SetTag(ResultTag, allowed ? Success : Failure);
}
