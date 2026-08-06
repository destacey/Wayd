namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Marks a command or query that cannot run unless the caller's account is linked to an employee
/// record. <c>LinkedEmployeeMiddleware</c> enforces this before the handler is constructed, so a
/// handler carrying this marker may assume the link exists.
/// </summary>
/// <remarks>
/// Coarse-grained authorization is keyed on the user (<c>MustHavePermission</c>), while fine-grained
/// authorization — PPM role assignments, roadmap manager visibility, team membership — is keyed on the
/// employee. Nothing bridged the two, so a user could hold a resource permission and still be unable to
/// reach any code path for that resource, failing in whichever way the handler happened to be written:
/// a 500 from a guard clause, a "could not determine employee id" result, or silently empty data.
/// <para>
/// Apply this to <em>actor</em> requests — those that record who did something or that resolve the
/// caller's own assignments. Do NOT apply it to viewer queries that can degrade gracefully (a roadmap
/// list can show public entries; global search can omit a category) or to "my X" queries, where an
/// empty result is a truthful answer for someone with no employee record.
/// </para>
/// </remarks>
public interface IRequireLinkedEmployee;
