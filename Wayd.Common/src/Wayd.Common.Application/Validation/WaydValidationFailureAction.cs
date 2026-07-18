using FluentValidation.Results;
using Wolverine.FluentValidation;
using ValidationException = Wayd.Common.Application.Exceptions.ValidationException;

namespace Wayd.Common.Application.Validation;

/// <summary>
/// Wolverine FluentValidation failure hook. Replaces the framework default (which throws Wolverine's
/// own <c>ValidationException</c>) so validation failures surface as our
/// <see cref="Wayd.Common.Application.Exceptions.ValidationException"/> — the exact exception
/// <c>ExceptionMiddleware</c> maps to a 422 problem-details response. Keeps the HTTP contract identical
/// to the previous MediatR <c>ValidationBehavior</c>.
/// </summary>
/// <typeparam name="T">The message type being validated.</typeparam>
public sealed class WaydValidationFailureAction<T> : IFailureAction<T>
{
    public void Throw(T message, IReadOnlyList<ValidationFailure> failures)
        => throw new ValidationException(failures);
}
