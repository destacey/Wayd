using Microsoft.AspNetCore.Identity;
using NodaTime;

namespace Wayd.Infrastructure.Identity;

public class ApplicationRoleClaim : IdentityRoleClaim<string>
{
    public string? CreatedBy { get; init; }
    public Instant Created { get; init; }
}