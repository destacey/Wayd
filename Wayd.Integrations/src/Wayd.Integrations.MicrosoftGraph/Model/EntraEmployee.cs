using Ardalis.GuardClauses;
using Microsoft.Graph.Models;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Extensions;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Integrations.MicrosoftGraph.Model;

public sealed record EntraEmployee : IExternalEmployee
{
    public EntraEmployee(User user, bool normalizeNameCasing = false)
    {
        EmployeeNumber = Guard.Against.NullOrWhiteSpace(user.EmployeeId ?? user.Id);
        var firstName = Guard.Against.NullOrWhiteSpace(user.GivenName);
        var lastName = Guard.Against.NullOrWhiteSpace(user.Surname);
        if (normalizeNameCasing)
        {
            // Title-cases all-caps input (Entra tenants frequently store legal names in caps),
            // pass-through for already-cased names. See NameCasing for the heuristic.
            firstName = NameCasing.TitleCaseIfMostlyUpper(firstName)!;
            lastName = NameCasing.TitleCaseIfMostlyUpper(lastName)!;
        }
        Name = new PersonName(firstName, null, lastName);
        HireDate = user.HireDate is not null
            ? Instant.FromDateTimeOffset((DateTimeOffset)user.HireDate)
            : user.EmployeeHireDate is not null
                ? Instant.FromDateTimeOffset((DateTimeOffset)user.EmployeeHireDate)
                : null;
        Email = new Common.Models.EmailAddress(user.Mail ?? Guard.Against.NullOrWhiteSpace(user.UserPrincipalName));
        JobTitle = user.JobTitle;
        Department = user.Department;
        OfficeLocation = user.OfficeLocation;
        ManagerEmployeeNumber = user.Manager?.Id;
        IsActive = user.AccountEnabled ?? false;
        // Pass Microsoft Graph's free-form employeeType through verbatim. Customers configure it
        // in their tenant; we don't normalize.
        EmployeeType = string.IsNullOrWhiteSpace(user.EmployeeType) ? null : user.EmployeeType.Trim();
        Emails = ResolveWorkEmails(user.ProxyAddresses, Email);
    }

    public string EmployeeNumber { get; set; }
    public PersonName Name { get; set; }
    public Instant? HireDate { get; set; }
    public Common.Models.EmailAddress Email { get; set; }
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public string? OfficeLocation { get; set; }
    public string? ManagerEmployeeNumber { get; set; }
    public bool IsActive { get; set; }
    public string? EmployeeType { get; set; }
    public IReadOnlyList<ExternalEmployeeEmail> Emails { get; set; }

    /// <summary>
    /// Projects Entra's <c>proxyAddresses</c> into work addresses. Entries are prefixed with the
    /// protocol: <c>SMTP:</c> uppercase marks the primary, lowercase <c>smtp:</c> a secondary, and
    /// non-mail protocols (<c>X500:</c>, <c>SIP:</c>, <c>EUM:</c>) appear alongside them. The
    /// prefix comparison is deliberately case-sensitive — casing is the only thing distinguishing
    /// primary from secondary.
    /// </summary>
    private static IReadOnlyList<ExternalEmployeeEmail> ResolveWorkEmails(
        IEnumerable<string>? proxyAddresses,
        Common.Models.EmailAddress canonicalEmail)
    {
        List<ExternalEmployeeEmail> emails = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in proxyAddresses ?? [])
        {
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            var value = entry.Trim();
            var isPrimary = value.StartsWith("SMTP:", StringComparison.Ordinal);
            if (!isPrimary && !value.StartsWith("smtp:", StringComparison.Ordinal))
                continue;

            var address = value[5..].Trim();

            // Routing addresses Microsoft generates for every mailbox. Real enough to deliver to,
            // but nobody is referenced by one in another system, so they are noise here.
            if (address.EndsWith(".onmicrosoft.com", StringComparison.OrdinalIgnoreCase)
                || address.EndsWith(".microsoftonline.com", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!address.IsValidEmailAddressFormat())
                continue;

            if (seen.Add(address))
                emails.Add(new ExternalEmployeeEmail(new Common.Models.EmailAddress(address), isPrimary));
        }

        if (seen.Add(canonicalEmail.Value))
            emails.Add(new ExternalEmployeeEmail(canonicalEmail, IsPrimary: true));

        return emails;
    }
}
