using Wayd.Common.Application.Interfaces.ExternalWork;

namespace Wayd.Common.Application.Validators;

public sealed class IExternalIterationValidator<T> : CustomValidator<IExternalIteration<T>> where T : class
{
    public IExternalIterationValidator()
    {
        RuleFor(c => c.Id)
            .GreaterThan(0);

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(c => c.Type)
            .IsInEnum();

        When(c => c.Start.HasValue,
            () => RuleFor(c => c.Start)
                .NotEmpty());

        When(c => c.End.HasValue,
            () => RuleFor(c => c.End)
                .NotEmpty());

        When(c => c.TeamId.HasValue,
            () => RuleFor(c => c.TeamId)
                .NotEmpty());

        RuleFor(c => c.Metadata)
            .NotNull();

        if (typeof(T) == typeof(AzdoIterationMetadata))
        {
            RuleFor(x => ((IExternalIteration<AzdoIterationMetadata>)x).Metadata!)
                .SetValidator(new AzdoIterationMetadataValidator());
        }
    }
}
