using Wayd.Common.Application.Persistence;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Employees.Queries;

/// <summary>
/// Looks up an Employee by email (case-insensitive). Used by the User-to-Employee link path —
/// auth identity is keyed on email regardless of which PeopleSync connector is currently active.
/// </summary>
public sealed record GetEmployeeByEmailQuery(string Email) : IQuery<Guid?>;

public sealed class GetEmployeeByEmailQueryHandler : IQueryHandler<GetEmployeeByEmailQuery, Guid?>
{
    private readonly IWaydDbContext _waydDbContext;

    public GetEmployeeByEmailQueryHandler(IWaydDbContext waydDbContext)
    {
        _waydDbContext = waydDbContext;
    }

    public async Task<Guid?> Handle(GetEmployeeByEmailQuery request, CancellationToken cancellationToken)
    {
        // Compare the whole EmailAddress: Email is mapped with a value converter, so e.Email.Value is
        // untranslatable and throws at runtime. This form also matches the unique filtered index on Email.
        var email = new EmailAddress(request.Email);

        // Cast to Guid? or an unmatched email returns Guid.Empty, which callers' HasValue checks accept as
        // a real employee — including UserService's RequireEmployeeRecord registration gate.
        return await _waydDbContext.Employees
            .Where(e => e.Email == email)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
