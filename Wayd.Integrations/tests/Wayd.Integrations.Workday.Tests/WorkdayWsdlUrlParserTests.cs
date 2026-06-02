using FluentAssertions;
using Wayd.AppIntegration.Domain.Models.Workday;
using Xunit;

namespace Wayd.Integrations.Workday.Tests;

public class WorkdayWsdlUrlParserTests
{
    [Theory]
    [InlineData(
        "https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1?wsdl",
        "wd3-impl-services1.workday.com", "acme_corp1", "v46.1",
        "https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1")]
    [InlineData(
        "https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1",
        "wd3-impl-services1.workday.com", "acme_corp1", "v46.1",
        "https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1")]
    [InlineData(
        "https://wd5-services1.myworkday.com/ccx/service/big_co/Staffing/v44.2/",
        "wd5-services1.myworkday.com", "big_co", "v44.2",
        "https://wd5-services1.myworkday.com/ccx/service/big_co/Staffing/v44.2/")]
    public void TryParse_validWsdlUrls_returnsExpectedParts(
        string url, string expectedHost, string expectedTenant, string expectedVersion, string expectedSoapEndpoint)
    {
        var ok = WorkdayConnectionConfiguration.TryParse(url, out var parts);

        ok.Should().BeTrue();
        parts.ServiceHost.Should().Be(expectedHost);
        parts.TenantAlias.Should().Be(expectedTenant);
        parts.WsdlVersion.Should().Be(expectedVersion);
        parts.SoapEndpoint.Should().Be(expectedSoapEndpoint);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not-a-url")]
    [InlineData("ftp://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1")]
    [InlineData("https://wd3-impl-services1.workday.com/some/other/path")]
    [InlineData("https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Human_Resources/v46.1")] // wrong service
    [InlineData("https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/")]            // no version
    [InlineData("https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/46.1")]         // version missing 'v'
    public void TryParse_invalidUrls_returnsFalse(string? url)
    {
        var ok = WorkdayConnectionConfiguration.TryParse(url, out var parts);

        ok.Should().BeFalse();
        parts.Should().Be(default(WorkdayWsdlUrlParts));
    }

    [Fact]
    public void Constructor_validUrl_derivesEndpointParts()
    {
        var config = new WorkdayConnectionConfiguration(
            wsdlUrl: "https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1?wsdl",
            isuUsername: "wayd_isu@acme_corp1",
            isuPassword: "secret");

        config.ServiceHost.Should().Be("wd3-impl-services1.workday.com");
        config.TenantAlias.Should().Be("acme_corp1");
        config.WsdlVersion.Should().Be("v46.1");
        config.SoapEndpoint.Should().Be("https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1");
    }

    [Fact]
    public void Constructor_invalidUrl_throwsArgumentException()
    {
        // The configuration enforces its own invariant: an unparseable WsdlUrl can't produce a
        // valid configuration object. The command-layer validator catches this at the API
        // boundary; the ctor throw is the domain seatbelt for any path that bypasses the validator.
        var act = () => new WorkdayConnectionConfiguration(
            wsdlUrl: "not-a-url",
            isuUsername: "wayd_isu@acme_corp1",
            isuPassword: "secret");

        act.Should().Throw<ArgumentException>().WithMessage("*not a valid Workday Staffing endpoint URL*");
    }
}
