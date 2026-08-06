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
        // Email is mapped with a value converter, so EF can translate the property but not its .Value
        // sub-member — comparing e.Email.Value throws "could not be translated" at runtime. Comparing the
        // whole EmailAddress also matches the unique filtered index on Email, which INCLUDEs Id.
        var email = new EmailAddress(request.Email);

        // The Guid? cast is required: over a non-nullable Guid, FirstOrDefaultAsync returns Guid.Empty for
        // an unmatched email, which callers' HasValue checks accept as a real employee — including the
        // RequireEmployeeRecord registration gate in UserService.
        return await _waydDbContext.Employees
            .Where(e => e.Email == email)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
