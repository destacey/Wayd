namespace Wayd.Infrastructure.Auth.Permissions;

/// <summary>
/// Marks an action sent with a method other than GET that changes nothing, such as a search or an evaluation whose
/// input travels in a body, so a read-only token may call it (<see cref="TokenScopeRequirement"/>). Every other such
/// action is refused to a read-only token, whatever permission it names.
/// </summary>
/// <remarks>
/// Put it only on an action that writes nothing, directly or through anything it dispatches: it is the whole of what
/// stands between a read-only token and the action. A hosted MCP tool for the action must be advertised read-only too,
/// which <c>McpToolCatalogTests</c> checks.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ReadOnlyOperationAttribute : Attribute;
