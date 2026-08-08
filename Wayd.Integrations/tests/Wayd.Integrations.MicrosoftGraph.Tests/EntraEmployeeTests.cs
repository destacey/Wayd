using Bogus;
using FluentAssertions;
using Wayd.Integrations.MicrosoftGraph.Model;
using Wayd.Integrations.MicrosoftGraph.Tests.Data;
using Xunit;

namespace Wayd.Integrations.MicrosoftGraph.Tests;

/// <summary>
/// Projection tests for the Graph User → <see cref="EntraEmployee"/> conversion. The Graph models
/// are POCOs we don't control, so a regression where Microsoft renames or reshapes a property
/// would only surface as a runtime null today. These tests pin the contract we depend on.
/// </summary>
public class EntraEmployeeTests
{
    private static readonly Faker _faker = new();

    /// <summary>
    /// A random address on an RFC-reserved domain. The proxyAddresses tests build prefixed entries
    /// from the literal value, so they mint it here rather than reading it back off the generated
    /// user.
    /// </summary>
    private static string Address(string domain = "acme.example") =>
        $"{_faker.Random.AlphaNumeric(10)}@{domain}".ToLowerInvariant();

    [Fact]
    public void Constructor_simpleUser_projectsRequiredFields()
    {
        // Sanity: without the casing flag, names round-trip exactly. If this assertion ever
        // breaks, EntraEmployee or PersonName changed in a way that touches the unmodified path.
        var user = new GraphUserFaker().Generate();

        var employee = new EntraEmployee(user);

        employee.Name.FirstName.Should().Be(user.GivenName);
        employee.Name.LastName.Should().Be(user.Surname);
        employee.Email.Value.Should().Be(user.Mail);
        employee.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Constructor_employeeIdPresent_prefersEmployeeIdOverObjectId()
    {
        // EntraEmployee.EmployeeNumber ?? falls back to user.Id when EmployeeId is null. Both paths
        // matter — EmployeeId is what HR-driven tenants populate; Id is the GUID fallback for
        // tenants that don't.
        var user = new GraphUserFaker().WithEmployeeId("EMP123").Generate();

        var employee = new EntraEmployee(user);

        employee.EmployeeNumber.Should().Be("EMP123");
    }

    [Fact]
    public void Constructor_normalizeNameCasing_defaultOff_preservesAllCaps()
    {
        // The constructor defaults to off — the runner explicitly opts in based on the connection
        // setting. Tests that pre-date the toggle (and any callers I missed) keep the raw value.
        var user = new GraphUserFaker().WithName("AVERY", "MCDONALD").Generate();

        var employee = new EntraEmployee(user);

        employee.Name.FirstName.Should().Be("AVERY");
        employee.Name.LastName.Should().Be("MCDONALD");
    }

    [Fact]
    public void Constructor_normalizeNameCasing_on_titleCasesAllCapsNames()
    {
        // MCDONALD → McDonald (Mc inner-cap rule from NameCasing). This is the whole point of the
        // toggle — Entra tenants storing legal names in caps now look consistent next to manually-
        // entered or mixed-case sources.
        var user = new GraphUserFaker().WithName("AVERY", "MCDONALD").Generate();

        var employee = new EntraEmployee(user, normalizeNameCasing: true);

        employee.Name.FirstName.Should().Be("Avery");
        employee.Name.LastName.Should().Be("McDonald");
    }

    [Fact]
    public void Constructor_normalizeNameCasing_on_preservesMixedCaseInput()
    {
        // Critical guarantee: an admin who deliberately typed "d'Artagnan" or "van der Berg" doesn't
        // get their casing mangled. NameCasing's heuristic only triggers on mostly-uppercase input.
        var user = new GraphUserFaker().WithName("d'Artagnan", "van der Berg").Generate();

        var employee = new EntraEmployee(user, normalizeNameCasing: true);

        employee.Name.FirstName.Should().Be("d'Artagnan");
        employee.Name.LastName.Should().Be("van der Berg");
    }

    [Fact]
    public void Constructor_normalizeNameCasing_on_handlesApostropheAndHyphenPrefixes()
    {
        // The two most common edge cases worth pinning at the projection level (the helper's own
        // tests cover the full matrix). MARY-ANNE → Mary-Anne; O'BRIEN → O'Brien.
        var user = new GraphUserFaker().WithName("MARY-ANNE", "O'BRIEN").Generate();

        var employee = new EntraEmployee(user, normalizeNameCasing: true);

        employee.Name.FirstName.Should().Be("Mary-Anne");
        employee.Name.LastName.Should().Be("O'Brien");
    }

    [Fact]
    public void Constructor_proxyAddresses_collectsPrimaryAndSecondarySmtpAddresses()
    {
        // The tenant-migration shape: Exchange demotes the previous primary to a secondary rather
        // than dropping it, which is what makes the old address recoverable at all.
        var current = Address();
        var former = Address("acme-legacy.example");
        var user = new GraphUserFaker()
            .WithMail(current)
            .WithProxyAddresses($"SMTP:{current}", $"smtp:{former}")
            .Generate();

        var employee = new EntraEmployee(user);

        employee.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo([current, former]);
    }

    [Fact]
    public void Constructor_proxyAddresses_uppercaseSmtpMarksThePrimary()
    {
        // Casing of the prefix is the only thing distinguishing primary from secondary, so a
        // case-insensitive comparison here would silently lose the distinction.
        var current = Address();
        var former = Address("acme-legacy.example");
        var user = new GraphUserFaker()
            .WithMail(current)
            .WithProxyAddresses($"smtp:{former}", $"SMTP:{current}")
            .Generate();

        var employee = new EntraEmployee(user);

        employee.Emails.Should().ContainSingle(e => e.IsPrimary);
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be(current);
    }

    [Fact]
    public void Constructor_proxyAddresses_excludesNonSmtpProtocols()
    {
        // X500 and SIP values aren't email addresses at all — X500 in particular would throw in
        // EmailAddress's constructor.
        var current = Address();
        var user = new GraphUserFaker()
            .WithMail(current)
            .WithProxyAddresses(
                $"SMTP:{current}",
                "X500:/o=Acme/ou=Exchange/cn=Recipients/cn=avchen",
                $"SIP:{current}",
                "EUM:12345;phone-context=acme.example")
            .Generate();

        var employee = new EntraEmployee(user);

        employee.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo([current]);
    }

    [Fact]
    public void Constructor_proxyAddresses_excludesRoutingAddresses()
    {
        // MOERA addresses are auto-generated for every mailbox; nobody is referenced by one in
        // another system, so they are noise.
        var current = Address();
        var user = new GraphUserFaker()
            .WithMail(current)
            .WithProxyAddresses(
                $"SMTP:{current}",
                $"smtp:{Address("acmecorp.onmicrosoft.com")}",
                $"smtp:{Address("acmecorp.microsoftonline.com")}")
            .Generate();

        var employee = new EntraEmployee(user);

        employee.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo([current]);
    }

    [Fact]
    public void Constructor_proxyAddressesAbsent_fallsBackToTheCanonicalAddress()
    {
        // Unlicensed users have no proxyAddresses, and Graph is documented to return the property
        // empty in some tenants even when asked for it.
        var user = new GraphUserFaker().Generate();

        var employee = new EntraEmployee(user);

        employee.Emails.Should().ContainSingle();
        employee.Emails.Single().Email.Value.Should().Be(user.Mail);
        employee.Emails.Single().IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void Constructor_proxyAddresses_deduplicatesIgnoringCase()
    {
        var current = Address();
        var user = new GraphUserFaker()
            .WithMail(current)
            .WithProxyAddresses($"SMTP:{current}", $"smtp:{current.ToUpperInvariant()}")
            .Generate();

        var employee = new EntraEmployee(user);

        employee.Emails.Should().ContainSingle();
    }

    [Fact]
    public void Constructor_proxyAddressesOmitsTheMailAddress_stillIncludesIt()
    {
        // Employee.Email has to be in the collection, whatever proxyAddresses says.
        var user = new GraphUserFaker()
            .WithProxyAddresses($"smtp:{Address("acme-legacy.example")}")
            .Generate();

        var employee = new EntraEmployee(user);

        employee.Emails.Select(e => e.Email.Value).Should().Contain(user.Mail);
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be(user.Mail);
    }
}
