namespace Wayd.Common.Application.Identity.Roles;

public interface IRoleService : ITransientService
{
    Task<List<RoleListDto>> GetList(CancellationToken cancellationToken);

    Task<int> GetCount(CancellationToken cancellationToken);

    Task<bool> Exists(string roleName, string? excludeId);

    Task<RoleDto?> GetById(string id, CancellationToken cancellationToken);

    Task<RoleDto?> GetByIdWithPermissions(string roleId, CancellationToken cancellationToken);

    Task<string> CreateOrUpdate(CreateOrUpdateRoleCommand request);

    Task<Result> UpdatePermissions(UpdateRolePermissionsCommand request, CancellationToken cancellationToken);

    Task<string> Delete(string id);
}