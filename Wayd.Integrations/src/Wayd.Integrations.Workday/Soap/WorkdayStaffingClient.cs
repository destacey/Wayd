using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces.ExternalPeople;

namespace Wayd.Integrations.Workday.Soap;

/// <summary>
/// Hand-rolled SOAP client for Workday's Staffing Web Service. Exposes a single operation
/// (<c>Get_Workers</c>) shared by both the bulk-sync source and the init probe. Uses
/// <see cref="HttpClient"/> + <see cref="XDocument"/> rather than a generated proxy so we don't
/// carry per-version artifacts and so we can absorb forward/backward Workday compat naturally.
/// </summary>
/// <remarks>
/// Versioning strategy: the SOAP endpoint URL (carried on the connection) encodes the WWS version
/// the customer's tenant is on. We send the same envelope shape regardless of version — Workday's
/// WWS contract is forward+backward compatible for read operations across the supported window.
/// Field-level changes are absorbed by the response parser (missing element → null).
/// </remarks>
public sealed class WorkdayStaffingClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WorkdayStaffingClient> _logger;

    // Workday's two primary namespaces. The envelope name is suffixed with the WWS version on the
    // tenant endpoint — but since Workday accepts the bare 'urn:com.workday/bsvc' namespace for
    // request envelopes, we don't bake a version into the namespace at all.
    public static readonly XNamespace Wd = "urn:com.workday/bsvc";
    private static readonly XNamespace _soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace _wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private static readonly XNamespace _wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
    private const string WssPasswordTextType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText";

    public WorkdayStaffingClient(HttpClient httpClient, ILogger<WorkdayStaffingClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Calls <c>Get_Workers</c> against the supplied tenant. Single page only — caller paginates
    /// by passing <paramref name="page"/> until <see cref="GetWorkersResponse.TotalPages"/> is reached.
    /// </summary>
    public async Task<GetWorkersResponse> GetWorkers(
        WorkdayConnectionCredentials credentials,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var envelope = BuildGetWorkersEnvelope(credentials, page, pageSize);
        var responseXml = await PostAsync(credentials, envelope, cancellationToken);

        // Find all wd:Worker elements anywhere in the response — Workday nests them under
        // Response_Data which lives under the operation response, but using a descendant-search
        // here keeps us tolerant of envelope shape drift.
        var workers = responseXml.Descendants(Wd + "Worker").ToList();
        var pageInfo = responseXml.Descendants(Wd + "Response_Results").FirstOrDefault();
        var totalPages = ParseIntOrDefault(pageInfo?.Element(Wd + "Total_Pages")?.Value, 1);
        var totalResults = ParseIntOrDefault(pageInfo?.Element(Wd + "Total_Results")?.Value, workers.Count);

        return new GetWorkersResponse(workers, page, totalPages, totalResults);
    }

    private static XDocument BuildGetWorkersEnvelope(WorkdayConnectionCredentials credentials, int page, int pageSize)
    {
        var requestCriteria = new XElement(Wd + "Request_Criteria",
            new XElement(Wd + "Exclude_Inactive_Workers", credentials.IncludeInactive ? "0" : "1"));

        // Transaction_Log_Criteria filters Get_Workers to workers whose data changed since a given
        // timestamp. Only attach it when the runner asked for incremental — first runs and probes
        // omit it to get a full snapshot.
        if (credentials.IncrementalUpdatedFrom is { } since)
        {
            requestCriteria.Add(new XElement(Wd + "Transaction_Log_Criteria",
                new XElement(Wd + "Transaction_Date_Range_Data",
                    new XElement(Wd + "Updated_From", since.ToDateTimeUtc().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)))));
        }

        var responseGroup = new XElement(Wd + "Response_Group",
            new XElement(Wd + "Include_Reference", "1"),
            new XElement(Wd + "Include_Personal_Information", "1"),
            new XElement(Wd + "Include_Employment_Information", "1"),
            new XElement(Wd + "Include_Organizations", "1"),
            new XElement(Wd + "Include_Management_Chain_Data", "1"));

        var responseFilter = new XElement(Wd + "Response_Filter",
            new XElement(Wd + "Page", page.ToString(CultureInfo.InvariantCulture)),
            new XElement(Wd + "Count", pageSize.ToString(CultureInfo.InvariantCulture)));

        var getWorkers = new XElement(Wd + "Get_Workers_Request",
            new XAttribute(XNamespace.Xmlns + "wd", Wd.NamespaceName),
            requestCriteria,
            responseFilter,
            responseGroup);

        var security = BuildSecurityHeader(credentials.IsuUsername, credentials.IsuPassword);

        var envelope = new XElement(_soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", _soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "wd", Wd.NamespaceName),
            new XElement(_soap + "Header", security),
            new XElement(_soap + "Body", getWorkers));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), envelope);
    }

    private static XElement BuildSecurityHeader(string username, string password)
    {
        // WS-Security UsernameToken with PasswordText. Workday accepts plain-text passwords over
        // TLS (the tenant endpoint is HTTPS-only). This is the standard ISU auth path.
        return new XElement(_wsse + "Security",
            new XAttribute(XNamespace.Xmlns + "wsse", _wsse.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "wsu", _wsu.NamespaceName),
            new XAttribute(_soap + "mustUnderstand", "1"),
            new XElement(_wsse + "UsernameToken",
                new XElement(_wsse + "Username", username),
                new XElement(_wsse + "Password",
                    new XAttribute("Type", WssPasswordTextType),
                    password)));
    }

    private async Task<XDocument> PostAsync(WorkdayConnectionCredentials credentials, XDocument envelope, CancellationToken cancellationToken)
    {
        var content = new StringContent(envelope.Declaration + envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

        using var request = new HttpRequestMessage(HttpMethod.Post, credentials.SoapEndpoint)
        {
            Content = content,
        };
        // Workday respects the SOAPAction header even though it's optional in SOAP 1.1.
        request.Headers.Add("SOAPAction", "");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Workday returns SOAP Faults inside a 500-class body. Surface fault details so the
            // initializer can distinguish auth failures from other errors.
            var fault = TryReadFault(body);
            _logger.LogWarning("Workday SOAP call returned {StatusCode}: {Fault}", (int)response.StatusCode, fault ?? body);
            throw new WorkdaySoapException((int)response.StatusCode, fault ?? body);
        }

        try
        {
            return XDocument.Parse(body, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex)
        {
            throw new WorkdaySoapException((int)response.StatusCode, "Workday returned a non-XML response.", ex);
        }
    }

    private static string? TryReadFault(string body)
    {
        try
        {
            var doc = XDocument.Parse(body);
            var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
            if (fault is null) return null;

            var reason = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value
                ?? fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "Reason")?.Value;
            return reason?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static int ParseIntOrDefault(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
}

public sealed record GetWorkersResponse(
    IReadOnlyList<XElement> Workers,
    int Page,
    int TotalPages,
    int TotalResults);

public sealed class WorkdaySoapException : Exception
{
    public int StatusCode { get; }

    public WorkdaySoapException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public WorkdaySoapException(int statusCode, string message, Exception inner) : base(message, inner)
    {
        StatusCode = statusCode;
    }

    public bool IsAuthFailure => StatusCode is 401 or 403 ||
        Message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
        Message.Contains("invalid user", StringComparison.OrdinalIgnoreCase);
}
