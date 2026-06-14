using Ardalis.GuardClauses;

namespace Wayd.Common.Domain.Identity;

/// <summary>
/// The just-in-time provisioning policy for an <see cref="OidcProvider"/>, modeled
/// as a discriminated shape: either auto-registration is <b>disabled</b> (no other
/// setting is meaningful), or it is <b>enabled</b> with an employee-record gate and
/// a required default role.
/// </summary>
/// <remarks>
/// The dependent settings (<see cref="RequireEmployeeRecord"/>,
/// <see cref="DefaultRoleId"/>) are nullable and are non-null <i>exactly</i> while
/// <see cref="AllowAutoRegistration"/> is <c>true</c>. The constructor validates and
/// <b>rejects</b> any contradictory combination rather than silently coercing it —
/// a disabled policy with a dependent value, or an enabled policy missing its gate or
/// role, throws. The illegal state is unrepresentable, and a caller mistake surfaces
/// immediately instead of being papered over.
///
/// Owned by <see cref="OidcProvider"/> and flattened by EF into the provider's own
/// table (see the entity configuration). EF materializes via the parameterless ctor
/// and writes the columns directly, bypassing this validation; that is safe because
/// the DB only ever holds values that were written through the validating path.
/// <see cref="AllowAutoRegistration"/> is always a non-null column, which keeps EF
/// from reading the owned instance back as <c>null</c> when the dependent columns are.
/// </remarks>
public sealed class RegistrationPolicy
{
    // EF materializes via this parameterless ctor and sets the column values
    // directly — see the remarks on why that's safe.
    private RegistrationPolicy() { }

    // The single chokepoint for object state. Validates the invariant and throws on
    // any contradiction; it does not normalize bad input into a legal state.
    private RegistrationPolicy(bool allowAutoRegistration, bool? requireEmployeeRecord, string? defaultRoleId)
    {
        if (allowAutoRegistration)
        {
            if (requireEmployeeRecord is null)
            {
                throw new ArgumentNullException(nameof(requireEmployeeRecord),
                    "An enabled registration policy must specify the employee-record gate.");
            }

            // The default role is required when enabled — there is no implicit
            // fallback. Trimming is cosmetic input cleaning; a blank id is a missing
            // role and is rejected.
            DefaultRoleId = Guard.Against.NullOrWhiteSpace(defaultRoleId, nameof(defaultRoleId),
                "An enabled registration policy must specify a default role.").Trim();
            RequireEmployeeRecord = requireEmployeeRecord;
        }
        else
        {
            // A disabled policy carries no dependent settings; supplying one is a
            // caller error, not something to quietly discard.
            if (requireEmployeeRecord is not null)
            {
                throw new ArgumentException(
                    "A disabled registration policy cannot specify an employee-record gate.",
                    nameof(requireEmployeeRecord));
            }

            if (defaultRoleId is not null)
            {
                throw new ArgumentException(
                    "A disabled registration policy cannot specify a default role.",
                    nameof(defaultRoleId));
            }
        }

        AllowAutoRegistration = allowAutoRegistration;
    }

    /// <summary>
    /// Master switch. When <c>false</c>, an unknown user signing in for the first
    /// time is rejected and an admin must pre-create the account.
    /// </summary>
    public bool AllowAutoRegistration { get; private set; }

    /// <summary>
    /// Whether auto-registration is gated to users with a matching employee record.
    /// <c>null</c> exactly when auto-registration is disabled — the setting has no
    /// meaning then.
    /// </summary>
    public bool? RequireEmployeeRecord { get; private set; }

    /// <summary>
    /// The role id assigned to auto-created users. Always present within an enabled
    /// policy (it is required); <c>null</c> exactly when auto-registration is disabled.
    /// </summary>
    public string? DefaultRoleId { get; private set; }

    /// <summary>Auto-registration off. No user is provisioned on first sign-in.</summary>
    public static RegistrationPolicy Disabled() => new(false, null, null);

    /// <summary>
    /// Auto-registration on. Both the employee-record gate and the default role id
    /// are required; the role id is trimmed and rejected if blank.
    /// </summary>
    public static RegistrationPolicy Enabled(bool requireEmployeeRecord, string defaultRoleId) =>
        new(allowAutoRegistration: true, requireEmployeeRecord, defaultRoleId);

    /// <summary>
    /// Builds a policy from the flat fields that arrive at the entity boundary
    /// (commands). This is the one place the loose external shape is translated into
    /// the strict domain shape: when auto-registration is off the dependent inputs are
    /// irrelevant and dropped; when on they are required and validated by the ctor.
    /// The employee-record gate is nullable on the way in — null is only legal when
    /// auto-registration is disabled, and is rejected when enabled.
    /// </summary>
    public static RegistrationPolicy FromFlat(bool allowAutoRegistration, bool? requireEmployeeRecord, string? defaultRoleId) =>
        allowAutoRegistration
            ? Enabled(
                Guard.Against.Null(requireEmployeeRecord, nameof(requireEmployeeRecord),
                    "The employee-record gate is required when auto-registration is enabled."),
                Guard.Against.NullOrWhiteSpace(defaultRoleId, nameof(defaultRoleId),
                    "A default role is required when auto-registration is enabled."))
            : Disabled();
}
