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
public sealed class WorkdayStaffingClient(HttpClient httpClient, TimeProvider timeProvider, ILogger<WorkdayStaffingClient> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<WorkdayStaffingClient> _logger = logger;

    // Workday's two primary namespaces. The envelope name is suffixed with the WWS version on the
    // tenant endpoint — but since Workday accepts the bare 'urn:com.workday/bsvc' namespace for
    // request envelopes, we don't bake a version into the namespace at all.
    public static readonly XNamespace Wd = "urn:com.workday/bsvc";
    private static readonly XNamespace _soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace _wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private static readonly XNamespace _wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
    private const string WssPasswordTextType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText";

    /// <summary>
    /// The largest response body read from Workday. The cap stops an endpoint that streams without end from
    /// exhausting the API's memory, and is set well above real pages: each worker carries every organization it
    /// belongs to and its management chain, so a page of 200 from a large tenant exceeds 32 MB. Parsing roughly
    /// quadruples the body (UTF-16 string, then the document).
    /// </summary>
    public const int MaxResponseBytes = 256 * 1024 * 1024;

    /// <summary>Why nothing is sent to an endpoint that is not HTTPS.</summary>
    public const string NotHttpsMessage =
        "The connection's WSDL URL is not HTTPS, so the ISU password would be sent unencrypted. Edit the connection and enter its https:// WSDL URL.";

    /// <summary>
    /// How long the body may take to arrive once the headers have. The resilience pipeline's attempt timeout
    /// ends at the headers, so without this an endpoint sending a byte at a time could hold a sync open.
    /// </summary>
    public static readonly TimeSpan BodyReadTimeout = TimeSpan.FromSeconds(90);

    /// <summary>The most of a SOAP fault's reason passed to the caller.</summary>
    private const int MaxFaultLength = 500;

    /// <summary>The most of a failed response's body written to the log.</summary>
    private const int MaxLoggedBodyLength = 2000;

    /// <summary>Why a response larger than <see cref="MaxResponseBytes"/> is refused.</summary>
    public static readonly string TooLargeMessage =
        $"Workday's response is larger than the {MaxResponseBytes / (1024 * 1024)} MB limit.";

    /// <summary>
    /// Set on the request by whatever stops reading a failed response's body before the client does — the
    /// resilience pipeline's retry check — so the client can report why, since what is left of the stream cannot
    /// be read again.
    /// </summary>
    public static readonly HttpRequestOptionsKey<string> UnreadBodyReason = new("Wayd.Workday.UnreadBodyReason");

    /// <summary>
    /// Calls <c>Get_Workers</c> against the supplied tenant. Single page only — caller paginates
    /// by passing <paramref name="page"/> until <see cref="GetWorkersResponse.TotalPages"/> is reached.
    /// </summary>
    public async Task<GetWorkersResponse> GetWorkers(
        WorkdayRequestContext context,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var envelope = BuildGetWorkersEnvelope(context, page, pageSize, _timeProvider.GetUtcNow());
        var (responseXml, responseBytes) = await PostAsync(context, envelope, cancellationToken);

        // Find all wd:Worker elements anywhere in the response — Workday nests them under
        // Response_Data which lives under the operation response, but using a descendant-search
        // here keeps us tolerant of envelope shape drift.
        var workers = responseXml.Descendants(Wd + "Worker").ToList();
        var pageInfo = responseXml.Descendants(Wd + "Response_Results").FirstOrDefault();
        var totalPages = ParseIntOrDefault(pageInfo?.Element(Wd + "Total_Pages")?.Value, 1);
        var totalResults = ParseIntOrDefault(pageInfo?.Element(Wd + "Total_Results")?.Value, workers.Count);

        return new GetWorkersResponse(workers, page, totalPages, totalResults, responseBytes);
    }

    private static XDocument BuildGetWorkersEnvelope(WorkdayRequestContext context, int page, int pageSize, DateTimeOffset now)
    {
        var requestCriteria = new XElement(Wd + "Request_Criteria",
            new XElement(Wd + "Exclude_Inactive_Workers", context.IncludeInactive ? "0" : "1"));

        // Transaction_Log_Criteria_Data filters Get_Workers to workers whose data changed since a
        // given timestamp. Only attach it when the runner asked for incremental — first runs and
        // probes omit it to get a full snapshot.
        //
        // Two Workday gotchas:
        //   1) The element name is Transaction_Log_Criteria_DATA (with the _Data suffix) inside
        //      Worker_Request_Criteria. The bare "Transaction_Log_Criteria" lives only under
        //      Organization_Request_Criteria and Workday rejects it here as an invalid subelement.
        //   2) Updated_From and Updated_Through are validated as a pair — supplying one without
        //      the other returns "If one of Updated From or Updated Through contains a value,
        //      both are Required!". We pair the watermark with "now" to close the range.
        if (context.IncrementalUpdatedFrom is { } since)
        {
            requestCriteria.Add(new XElement(Wd + "Transaction_Log_Criteria_Data",
                new XElement(Wd + "Transaction_Date_Range_Data",
                    new XElement(Wd + "Updated_From", since.ToDateTimeUtc().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
                    new XElement(Wd + "Updated_Through", now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)))));
        }

        // Response_Group controls how much Workday serializes per worker — the dominant driver of
        // Get_Workers response time on large tenants. We request the four data areas the projector
        // reads (reference, personal, employment, organizations) plus the management chain (the only
        // place the direct-manager link lives).
        //
        // NOTE: org-data is the expensive area (Include_Organizations returns every org a worker is in
        // plus full parent hierarchies). Workday offers Exclude_* sub-flags to trim it, but we can't
        // blanket-apply them: the connector's Department source and exclusion rules both match on an
        // admin-chosen Organization_Type_ID, and that catalog includes BOTH base org types AND
        // hierarchy types — so any sub-category (including a *_Hierarchy) may be the exact data a
        // tenant's config depends on. Excluding it would silently null departments / disable
        // exclusions. Safe trimming has to be computed from the connection's config (only exclude
        // sub-categories provably unused by this connector), which is a follow-up.
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

        var security = BuildSecurityHeader(context.Credentials);

        var envelope = new XElement(_soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", _soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "wd", Wd.NamespaceName),
            new XElement(_soap + "Header", security),
            new XElement(_soap + "Body", getWorkers));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), envelope);
    }

    private static XElement BuildSecurityHeader(WorkdayCredentials credentials)
    {
        // WS-Security UsernameToken with PasswordText. Workday accepts plain-text passwords over
        // TLS (the tenant endpoint is HTTPS-only). This is the standard ISU auth path.
        return new XElement(_wsse + "Security",
            new XAttribute(XNamespace.Xmlns + "wsse", _wsse.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "wsu", _wsu.NamespaceName),
            new XAttribute(_soap + "mustUnderstand", "1"),
            new XElement(_wsse + "UsernameToken",
                new XElement(_wsse + "Username", credentials.IsuUsername),
                new XElement(_wsse + "Password",
                    new XAttribute("Type", WssPasswordTextType),
                    credentials.IsuPassword)));
    }

    /// <summary>
    /// Calls <c>Get_Organizations</c> to enumerate every organization in the tenant and group by
    /// <c>Organization_Type_ID</c>. Used by the init probe to populate the catalog of org-types the
    /// admin can pick from when mapping <c>Employee.Department</c>. Single page only — we cap by
    /// asking for a generous Count (default 999, Workday's per-call max) since the org catalog is
    /// usually small (tens to low thousands).
    /// </summary>
    public async Task<GetOrganizationsResponse> GetOrganizations(
        WorkdayRequestContext context,
        int page,
        int pageSize,
        CancellationToken cancellationToken,
        string? organizationTypeId = null)
    {
        var envelope = BuildGetOrganizationsEnvelope(context, page, pageSize, organizationTypeId);
        var (responseXml, _) = await PostAsync(context, envelope, cancellationToken);

        // Each Organization element carries Reference_ID, Name, and Organization_Type_Reference.
        var orgs = responseXml.Descendants(Wd + "Organization").ToList();
        var pageInfo = responseXml.Descendants(Wd + "Response_Results").FirstOrDefault();
        var totalPages = ParseIntOrDefault(pageInfo?.Element(Wd + "Total_Pages")?.Value, 1);
        var totalResults = ParseIntOrDefault(pageInfo?.Element(Wd + "Total_Results")?.Value, orgs.Count);

        return new GetOrganizationsResponse(orgs, page, totalPages, totalResults);
    }

    private static XDocument BuildGetOrganizationsEnvelope(WorkdayRequestContext context, int page, int pageSize, string? organizationTypeId)
    {
        // Request_Criteria is empty by default — we want everything so we can group by type.
        // When organizationTypeId is supplied (the lazy-load endpoint for the exclusions picker),
        // we narrow with an Organization_Type_Reference filter so we don't fetch the entire tenant
        // catalog just to enumerate orgs of one type.
        var requestCriteria = new XElement(Wd + "Request_Criteria");
        if (!string.IsNullOrWhiteSpace(organizationTypeId))
        {
            requestCriteria.Add(new XElement(Wd + "Organization_Type_Reference",
                new XElement(Wd + "ID",
                    new XAttribute(Wd + "type", "Organization_Type_ID"),
                    organizationTypeId)));
        }

        // No response-group flags — the default response returns Reference + Name + Type for each
        // org, which is everything the catalog needs. Asking for Hierarchy/Supervisory data here
        // would bloat the response without buying anything for the picker.
        var responseFilter = new XElement(Wd + "Response_Filter",
            new XElement(Wd + "Page", page.ToString(CultureInfo.InvariantCulture)),
            new XElement(Wd + "Count", pageSize.ToString(CultureInfo.InvariantCulture)));

        var getOrgs = new XElement(Wd + "Get_Organizations_Request",
            new XAttribute(XNamespace.Xmlns + "wd", Wd.NamespaceName),
            requestCriteria,
            responseFilter);

        var security = BuildSecurityHeader(context.Credentials);

        var envelope = new XElement(_soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", _soap.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "wd", Wd.NamespaceName),
            new XElement(_soap + "Header", security),
            new XElement(_soap + "Body", getOrgs));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), envelope);
    }

    /// <summary>Sends <paramref name="envelope"/> and returns the parsed response with its size in bytes.</summary>
    private async Task<(XDocument Document, long Bytes)> PostAsync(WorkdayRequestContext context, XDocument envelope, CancellationToken cancellationToken)
    {
        // The envelope carries the ISU password in plain text. A connection saved before WSDL URLs had to be
        // HTTPS still holds its http:// endpoint, so the scheme is checked on every send, not only on save.
        if (!Uri.TryCreate(context.SoapEndpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
            throw new WorkdaySoapException(0, NotHttpsMessage);

        var content = new StringContent(envelope.Declaration + envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
        content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

        using var request = new HttpRequestMessage(HttpMethod.Post, context.SoapEndpoint)
        {
            Content = content,
        };
        // Workday respects the SOAPAction header even though it's optional in SOAP 1.1.
        request.Headers.Add("SOAPAction", "");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var statusCode = (int)response.StatusCode;

        var body = await ReadBody(response, statusCode, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Workday returns SOAP Faults inside a 500-class body; the fault's reason lets the initializer tell
            // an auth failure from other errors. Anything else in the body stays in the log: the caller learns
            // only the status, so the connection cannot be used to read what some other server answered.
            var fault = TryReadFault(body);
            _logger.LogWarning("Workday SOAP call returned {StatusCode}: {Body}", statusCode, Truncate(fault ?? body, MaxLoggedBodyLength));
            throw new WorkdaySoapException(statusCode, fault is null
                ? $"Workday returned HTTP {statusCode} without a SOAP fault."
                : Truncate(fault, MaxFaultLength));
        }

        try
        {
            // Once buffered, the content's length is the buffer's, whether or not Workday declared one.
            var bytes = response.Content.Headers.ContentLength ?? Encoding.UTF8.GetByteCount(body);
            return (XDocument.Parse(body, LoadOptions.PreserveWhitespace), bytes);
        }
        catch (XmlException ex)
        {
            throw new WorkdaySoapException((int)response.StatusCode, "Workday returned a non-XML response.", ex);
        }
    }

    /// <summary>
    /// Reads the response body, refusing one larger than <see cref="MaxResponseBytes"/> or slower than
    /// <see cref="BodyReadTimeout"/>. Every way the read can fail becomes a <see cref="WorkdaySoapException"/>,
    /// the one exception the sync and the probe report rather than let escape.
    /// </summary>
    private static async Task<string> ReadBody(HttpResponseMessage response, int statusCode, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is > MaxResponseBytes and var declared)
            throw new WorkdaySoapException(statusCode, $"{TooLargeMessage} It declared {declared / (1024.0 * 1024):F0} MB.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(BodyReadTimeout);
        try
        {
            // Buffering with a limit stops reading at the cap even when no length was declared.
            await response.Content.LoadIntoBufferAsync(MaxResponseBytes, timeout.Token);
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.ConfigurationLimitExceeded)
        {
            throw new WorkdaySoapException(statusCode, TooLargeMessage, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new WorkdaySoapException(statusCode, "Workday's response was cut off before it arrived in full.", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WorkdaySoapException(statusCode, $"Workday's response did not arrive in full within {BodyReadTimeout.TotalSeconds:F0} seconds.", ex);
        }
        catch (InvalidOperationException ex)
        {
            // The resilience pipeline's retry check started reading this body and could not finish, so what is
            // left of the stream cannot be buffered.
            var reason = response.RequestMessage?.Options.TryGetValue(UnreadBodyReason, out var recorded) == true
                ? recorded
                : "Workday's response could not be read in full.";
            throw new WorkdaySoapException(statusCode, reason, ex);
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "…");

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

    /// <summary>
    /// Classifies a non-success Workday SOAP response body as retryable or not. Workday wraps
    /// semantic faults — bad ISU credentials, malformed requests, missing-permission errors — in
    /// HTTP 500 bodies, which the resilience handler's default predicate would otherwise treat as
    /// transient and retry 3× to no effect. We parse the SOAP fault and refuse retries for faults
    /// that re-running can't fix (auth + validation). Anything we can't classify (no parseable
    /// fault, genuine transient server error) falls through to the default transient handling.
    /// </summary>
    /// <returns><c>true</c> when the fault is permanent and the request should NOT be retried.</returns>
    public static bool IsNonRetryableFault(string body)
    {
        var fault = TryReadFault(body);
        if (string.IsNullOrWhiteSpace(fault)) return false;

        // Auth failures: same heuristics WorkdaySoapException.IsAuthFailure uses. Re-running with
        // the same bad credentials will never succeed. Match on the "authenticat" stem so all of
        // authenticate / authenticated / authentication classify the same (Workday phrases these
        // inconsistently, e.g. "could not be authenticated").
        if (fault.Contains("authenticat", StringComparison.OrdinalIgnoreCase)
            || fault.Contains("invalid user", StringComparison.OrdinalIgnoreCase)
            || fault.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
            return true;

        // Validation faults: Workday rejects malformed envelopes / invalid criteria with
        // "Validation error" / "invalid" / "is not a valid" reasons. These are deterministic —
        // the same request will fail identically every time.
        if (fault.Contains("validation error", StringComparison.OrdinalIgnoreCase)
            || fault.Contains("is not a valid", StringComparison.OrdinalIgnoreCase)
            || fault.Contains("invalid request", StringComparison.OrdinalIgnoreCase)
            || fault.Contains("processing error", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static int ParseIntOrDefault(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
}

/// <summary>One page of <c>Get_Workers</c>.</summary>
/// <param name="Workers">The page's <c>wd:Worker</c> elements.</param>
/// <param name="Page">The page number requested.</param>
/// <param name="TotalPages">The page count Workday reports.</param>
/// <param name="TotalResults">The worker count Workday reports.</param>
/// <param name="ResponseBytes">The size of the response body, to compare with <see cref="WorkdayStaffingClient.MaxResponseBytes"/>.</param>
public sealed record GetWorkersResponse(
    IReadOnlyList<XElement> Workers,
    int Page,
    int TotalPages,
    int TotalResults,
    long ResponseBytes);

public sealed record GetOrganizationsResponse(
    IReadOnlyList<XElement> Organizations,
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
