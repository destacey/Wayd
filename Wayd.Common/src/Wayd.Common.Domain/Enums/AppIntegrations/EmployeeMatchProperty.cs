namespace Wayd.Common.Domain.Enums.AppIntegrations;

/// <summary>
/// Which uniquely-indexed field on <c>Employee</c> a PeopleSync connection uses to find an
/// existing row during upsert. Lets admins control how a tenant's employee identity is keyed
/// across connector switches (e.g. an Entra-then-Workday migration uses <see cref="Email"/> on
/// the new Workday connection so the first sync collapses onto existing rows).
/// </summary>
public enum EmployeeMatchProperty
{
    /// <summary>Match on <c>Employee.Email</c> (case-insensitive). The cross-source-stable choice.</summary>
    Email = 0,

    /// <summary>Match on <c>Employee.EmployeeNumber</c>. Use when both connectors emit the same stable employee number.</summary>
    EmployeeNumber = 1,
}
