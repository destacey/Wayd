using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Wayd.Integrations.Workday.Soap;

/// <summary>
/// Helper for reading Workday Worker fields with a list of candidate XPath expressions.
/// First non-empty match wins. Used by both the bulk-sync parser and the init probe — the parser
/// extracts data; the probe inspects field presence to detect ISSG permission gaps.
/// </summary>
internal static class WorkerFieldReader
{
    private static readonly IXmlNamespaceResolver _ns = WorkdayNamespaceResolver.Instance;

    /// <summary>Returns the first non-empty string value matching any of <paramref name="xpathCandidates"/>.</summary>
    public static string? GetValue(XElement worker, params string[] xpathCandidates)
    {
        foreach (var xpath in xpathCandidates)
        {
            var node = worker.XPathSelectElement(xpath, _ns);
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
            if (worker.XPathSelectElement(xpath, _ns) is not null)
                return true;
        }
        return false;
    }

    /// <summary>Returns the first <see cref="XElement"/> matching any candidate XPath, or null.</summary>
    public static XElement? GetElement(XElement worker, params string[] xpathCandidates)
    {
        foreach (var xpath in xpathCandidates)
        {
            var node = worker.XPathSelectElement(xpath, _ns);
            if (node is not null)
                return node;
        }
        return null;
    }

    /// <summary>Resolves an attribute on the first matching candidate XPath.</summary>
    public static string? GetAttributeValue(XElement worker, string attributeLocalName, params string[] xpathCandidates)
    {
        foreach (var xpath in xpathCandidates)
        {
            var node = worker.XPathSelectElement(xpath, _ns);
            if (node is null) continue;

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
