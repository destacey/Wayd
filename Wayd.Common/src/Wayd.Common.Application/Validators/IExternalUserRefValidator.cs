using Wayd.Common.Application.Interfaces.ExternalWork;

namespace Wayd.Common.Application.Validators;

public sealed class IExternalUserRefValidator : CustomValidator<IExternalUserRef>
{
    public IExternalUserRefValidator()
    {
        // The identity key. A connector that cannot supply one has nothing stable to map against,
        // so the reference is rejected rather than silently resolving to nobody.
        RuleFor(c => c.ExternalId)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.Email)
            .MaximumLength(256);

        RuleFor(c => c.DisplayName)
            .MaximumLength(256);

        RuleFor(c => c.Handle)
            .MaximumLength(256);
    }
}
