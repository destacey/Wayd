using FluentAssertions;
using Wayd.Integrations.Workday.Soap;
using Xunit;

namespace Wayd.Integrations.Workday.Tests;

/// <summary>
/// Covers <see cref="WorkdayStaffingClient.IsNonRetryableFault"/>, the predicate the per-client
/// resilience handler uses to suppress retries on Workday's permanent (auth/validation) faults.
/// Workday wraps these in HTTP 500 bodies that the default transient predicate would otherwise
/// retry 3× to no effect. Fault bodies here are composed from first principles, not real payloads.
/// </summary>
public class WorkdayFaultClassificationTests
{
    // Minimal SOAP 1.1 fault envelope. faultstring carries the human-readable reason that Workday
    // populates on validation/auth failures; that's what the classifier keys off.
    private static string FaultBody(string faultString) =>
        $"""
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
          <soapenv:Body>
            <soapenv:Fault>
              <faultcode>soapenv:Server</faultcode>
              <faultstring>{faultString}</faultstring>
            </soapenv:Fault>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    [Theory]
    [InlineData("Invalid username or password. The request could not be authenticated.")]
    [InlineData("Authentication failed for the supplied credentials.")]
    [InlineData("Unauthorized: the integration system user lacks the required security group.")]
    // Stem coverage: "authenticated" with no "user"/"unauthorized" substring must still classify —
    // it only matches via the "authenticat" stem, not the longer "authentication".
    [InlineData("The request could not be authenticated.")]
    [InlineData("Unable to authenticate the integration system security group.")]
    public void IsNonRetryableFault_authFault_returnsTrue(string reason)
    {
        WorkdayStaffingClient.IsNonRetryableFault(FaultBody(reason)).Should().BeTrue();
    }

    [Theory]
    [InlineData("Validation error processing the Get_Workers request.")]
    [InlineData("The value 'XYZ' is not a valid Organization_Type_ID.")]
    [InlineData("Invalid request: Updated_From and Updated_Through must both be supplied.")]
    [InlineData("Processing error occurred while evaluating the request criteria.")]
    public void IsNonRetryableFault_validationFault_returnsTrue(string reason)
    {
        WorkdayStaffingClient.IsNonRetryableFault(FaultBody(reason)).Should().BeTrue();
    }

    [Theory]
    [InlineData("The server is temporarily unavailable. Please try again later.")]
    [InlineData("An unexpected internal error occurred.")]
    [InlineData("Service timeout while contacting the persistence layer.")]
    public void IsNonRetryableFault_transientLookingFault_returnsFalse(string reason)
    {
        // Faults we can't positively classify as permanent fall through to the default transient
        // handling so genuine server hiccups still get retried.
        WorkdayStaffingClient.IsNonRetryableFault(FaultBody(reason)).Should().BeFalse();
    }

    [Fact]
    public void IsNonRetryableFault_nonFaultBody_returnsFalse()
    {
        // A successful-looking Get_Workers body (no Fault element) is not a permanent failure.
        var body = """
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:wd="urn:com.workday/bsvc">
              <soapenv:Body>
                <wd:Get_Workers_Response>
                  <wd:Response_Results><wd:Total_Pages>1</wd:Total_Pages></wd:Response_Results>
                </wd:Get_Workers_Response>
              </soapenv:Body>
            </soapenv:Envelope>
            """;

        WorkdayStaffingClient.IsNonRetryableFault(body).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this is not xml at all")]
    [InlineData("<unclosed>")]
    public void IsNonRetryableFault_emptyOrUnparseableBody_returnsFalse(string body)
    {
        // Unparseable bodies can't be classified; default to retryable rather than swallowing a
        // potentially-transient failure.
        WorkdayStaffingClient.IsNonRetryableFault(body).Should().BeFalse();
    }
}
