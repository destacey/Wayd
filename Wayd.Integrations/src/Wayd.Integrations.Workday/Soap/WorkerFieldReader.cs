using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Wayd.Integrations.Workday.Soap;

/// <summary>
/// Helper for reading Workday Worker fields with a list of candidate XPath expressions.
/// First non-empty match wins. Used by both the bulk-sync parser and the init probe — the parser
/// extracts data; the probe inspects field presence to detect ISSG permission gaps.
/// </summary>
/// <remarks>
/// Performance: <see cref="Extensions.XPathSelectElement(XNode, string)"/> recompiles the XPath
/// string on every call. A bulk sync evaluates ~18 fields per worker across hundreds of workers,
/// so that recompilation dominated the projection cost. We compile each distinct XPath once into a
/// reusable <see cref="XPathExpression"/> and cache it. Compiled expressions aren't safe for
/// concurrent evaluation, so each lookup hands back a cheap <see cref="XPathExpression.Clone"/>
/// rather than the shared instance — Clone is orders of magnitude cheaper than Compile.
/// </remarks>
internal static class WorkerFieldReader
{
    private static readonly IXmlNamespaceResolver _ns = WorkdayNamespaceResolver.Instance;

    // Keyed by the raw XPath string. Bounded by the number of distinct field paths (~20), so this
    // grows to a small fixed size and never needs eviction.
    private static readonly ConcurrentDictionary<string, XPathExpression> _compiled = new();

    /// <summary>
    /// Returns the first node matching <paramref name="xpath"/> using a cached compiled expression,
    /// or null. Centralizes the compile-once-and-clone pattern so every reader method benefits.
    /// </summary>
    private static XPathNavigator? SelectSingleNode(XElement element, string xpath)
    {
        var expr = _compiled.GetOrAdd(xpath, static x => XPathExpression.Compile(x, _ns));
        var navigator = element.CreateNavigator();
        // Clone so concurrent syncs (or future parallelism) never share a live expression context.
        return navigator.SelectSingleNode(expr.Clone());
    }

    /// <summary>
    /// Enumerates the trimmed, non-empty string values of every node matching <paramref name="xpath"/>,
    /// using a cached compiled expression. For multi-valued reads (e.g. a worker's org references)
    /// where the caller needs to inspect all matches rather than just the first.
    /// </summary>
    public static IEnumerable<string> SelectValues(XElement element, string xpath)
    {
        var expr = _compiled.GetOrAdd(xpath, static x => XPathExpression.Compile(x, _ns));
        var navigator = element.CreateNavigator();
        var iterator = navigator.Select(expr.Clone());
        while (iterator.MoveNext())
        {
            var value = iterator.Current?.Value?.Trim();
            if (!string.IsNullOrEmpty(value))
                yield return value;
        }
    }

    /// <summary>Returns the first non-empty string value matching any of <paramref name="xpathCandidates"/>.</summary>
    public static string? GetValue(XElement worker, params string[] xpathCandidates)
    {
        foreach (var xpath in xpathCandidates)
        {
            var node = SelectSingleNode(worker, xpath);
            var value = node?.Value?.Trim();
            if (!string.IsNullOrEmpty(value))
                return value;
        }
        return null;
    }

    /// <summary>
    /// Returns true when at least one element matching any candidate XPath exists in the worker
    /// envelope — regardless of whether its value is empty. Used by the probe to distinguish
    /// "ISSG didn't grant this field" (element absent) from "field is empty for this worker"
    /// (element present, value blank).
    /// </summary>
    public static bool HasElement(XElement worker, params string[] xpathCandidates)
    {
        foreach (var xpath in xpathCandidates)
        {
            if (SelectSingleNode(worker, xpath) is not null)
                return true;
        }
        return false;
    }

    /// <summary>Returns the first <see cref="XElement"/> matching any candidate XPath, or null.</summary>
    public static XElement? GetElement(XElement worker, params string[] xpathCandidates)
    {
        foreach (var xpath in xpathCandidates)
        {
            // SelectSingleNode returns an XPathNavigator positioned on the match; UnderlyingObject
            // gives back the original XElement the navigator was created over.
            if (SelectSingleNode(worker, xpath)?.UnderlyingObject is XElement node)
                return node;
        }
        return null;
    }

    /// <summary>Resolves an attribute on the first matching candidate XPath.</summary>
    public static string? GetAttributeValue(XElement worker, string attributeLocalName, params string[] xpathCandidates)
    {
        foreach (var xpath in xpathCandidates)
        {
            if (SelectSingleNode(worker, xpath)?.UnderlyingObject is not XElement node)
                continue;

            // Match by local name to avoid pinning the attribute namespace.
            var attr = node.Attributes().FirstOrDefault(a => a.Name.LocalName == attributeLocalName);
            if (attr is not null && !string.IsNullOrWhiteSpace(attr.Value))
                return attr.Value.Trim();
        }
        return null;
    }
}

/// <summary>Bound namespace prefixes used by XPath expressions targeting Workday SOAP responses.</summary>
internal sealed class WorkdayNamespaceResolver : IXmlNamespaceResolver
{
    public static readonly WorkdayNamespaceResolver Instance = new();

    private readonly Dictionary<string, string> _map = new()
    {
        ["wd"] = WorkdayStaffingClient.Wd.NamespaceName,
    };

    public IDictionary<string, string> GetNamespacesInScope(XmlNamespaceScope scope) => _map;
    public string? LookupNamespace(string prefix) => _map.TryGetValue(prefix, out var ns) ? ns : null;
    public string? LookupPrefix(string namespaceName) => _map.FirstOrDefault(kv => kv.Value == namespaceName).Key;
}
