using FluentAssertions;
using Microsoft.Graph.Models;
using Wayd.Integrations.MicrosoftGraph.Model;
using Xunit;

namespace Wayd.Integrations.MicrosoftGraph.Tests;

/// <summary>
/// Projection tests for the Graph User → <see cref="EntraEmployee"/> conversion. The Graph models
/// are POCOs we don't control, so a regression where Microsoft renames or reshapes a property
/// would only surface as a runtime null today. These tests pin the contract we depend on.
/// </summary>
public class EntraEmployeeTests
{
    private static User BuildUser(
        string givenName = "Daniel",
        string surname = "Stacey",
        string id = "11111111-1111-1111-1111-111111111111",
        string mail = "daniel.stacey@acme.example",
        string? employeeId = null) => new()
        {
            Id = id,
            EmployeeId = employeeId,
            GivenName = givenName,
            Surname = surname,
            Mail = mail,
            AccountEnabled = true,
        };

    [Fact]
    public void Constructor_simpleUser_projectsRequiredFields()
    {
        // Sanity: without the casing flag, names round-trip exactly. If this assertion ever
        // breaks, EntraEmployee or PersonName changed in a way that touches the unmodified path.
        var user = BuildUser();

        var employee = new EntraEmployee(user);

        employee.Name.FirstName.Should().Be("Daniel");
        employee.Name.LastName.Should().Be("Stacey");
        employee.Email.Value.Should().Be("daniel.stacey@acme.example");
        employee.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Constructor_employeeIdPresent_prefersEmployeeIdOverObjectId()
    {
        // EntraEmployee.EmployeeNumber ?? falls back to user.Id when EmployeeId is null. Both paths
        // matter — EmployeeId is what HR-driven tenants populate; Id is the GUID fallback for
        // tenants that don't.
        var user = BuildUser(employeeId: "EMP123");

        var employee = new EntraEmployee(user);

        employee.EmployeeNumber.Should().Be("EMP123");
    }

    [Fact]
    public void Constructor_normalizeNameCasing_defaultOff_preservesAllCaps()
    {
        // The constructor defaults to off — the runner explicitly opts in based on the connection
        // setting. Tests that pre-date the toggle (and any callers I missed) keep the raw value.
        var user = BuildUser(givenName: "DANIEL", surname: "MCDONALD");

        var employee = new EntraEmployee(user);

        employee.Name.FirstName.Should().Be("DANIEL");
        employee.Name.LastName.Should().Be("MCDONALD");
    }

    [Fact]
    public void Constructor_normalizeNameCasing_on_titleCasesAllCapsNames()
    {
        // MCDONALD → McDonald (Mc inner-cap rule from NameCasing). This is the whole point of the
        // toggle — Entra tenants storing legal names in caps now look consistent next to manually-
        // entered or mixed-case sources.
        var user = BuildUser(givenName: "DANIEL", surname: "MCDONALD");

        var employee = new EntraEmployee(user, normalizeNameCasing: true);

        employee.Name.FirstName.Should().Be("Daniel");
        employee.Name.LastName.Should().Be("McDonald");
    }

    [Fact]
    public void Constructor_normalizeNameCasing_on_preservesMixedCaseInput()
    {
        // Critical guarantee: an admin who deliberately typed "d'Artagnan" or "van der Berg" doesn't
        // get their casing mangled. NameCasing's heuristic only triggers on mostly-uppercase input.
        var user = BuildUser(givenName: "d'Artagnan", surname: "van der Berg");

        var employee = new EntraEmployee(user, normalizeNameCasing: true);

        employee.Name.FirstName.Should().Be("d'Artagnan");
        employee.Name.LastName.Should().Be("van der Berg");
    }

    [Fact]
    public void Constructor_normalizeNameCasing_on_handlesApostropheAndHyphenPrefixes()
    {
        // The two most common edge cases worth pinning at the projection level (the helper's own
        // tests cover the full matrix). MARY-ANNE → Mary-Anne; O'BRIEN → O'Brien.
        var user = BuildUser(givenName: "MARY-ANNE", surname: "O'BRIEN");

        var employee = new EntraEmployee(user, normalizeNameCasing: true);

        employee.Name.FirstName.Should().Be("Mary-Anne");
        employee.Name.LastName.Should().Be("O'Brien");
    }
}
