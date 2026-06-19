namespace Wayd.Common.Application.Identity.Users;

public sealed class UserListFilter : PaginationFilter
{
    public bool? IsActive { get; set; }
}