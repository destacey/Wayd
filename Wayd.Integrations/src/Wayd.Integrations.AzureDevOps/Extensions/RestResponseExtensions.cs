using RestSharp;

namespace Wayd.Integrations.AzureDevOps.Extensions;

internal static class RestResponseExtensions
{
    private const int MaxContentLength = 500;

    /// <summary>
    /// Builds a diagnostic error string from a failed response. RestSharp only populates
    /// <see cref="RestResponseBase.ErrorMessage"/> for transport-level exceptions; for HTTP-level
    /// failures the useful detail (status code and the Azure DevOps error body) lives in
    /// StatusCode/Content, so include all three when present.
    /// </summary>
    internal static string GetErrorText(this RestResponse response)
    {
        var parts = new List<string>(3)
        {
            response.StatusCode == 0 ? "Connection Error" : $"{(int)response.StatusCode} {response.StatusDescription}"
        };

        if (!string.IsNullOrWhiteSpace(response.ErrorMessage))
            parts.Add(response.ErrorMessage);

        if (!string.IsNullOrWhiteSpace(response.Content))
        {
            var content = response.Content.Trim();
            parts.Add(content.Length <= MaxContentLength ? content : content[..MaxContentLength] + "…");
        }

        return string.Join(" - ", parts);
    }
}
