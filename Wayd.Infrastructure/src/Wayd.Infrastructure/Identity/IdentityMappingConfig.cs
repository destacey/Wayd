using Mapster;
using Wayd.Common.Application.Dtos;
using NodaTime;

namespace Wayd.Infrastructure.Identity;

public class IdentityMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config
            .NewConfig<ApplicationUser, UserDetailsDto>()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.UserName, src => src.UserName)
                .Map(dest => dest.FirstName, src => src.FirstName)
                .Map(dest => dest.LastName, src => src.LastName)
                .Map(dest => dest.Email, src => src.Email)
                .Map(dest => dest.PhoneNumber, src => src.PhoneNumber)
                .Map(dest => dest.LoginProvider, src => src.LoginProvider)
                .Map(dest => dest.PendingMigrationTenantId, src => src.PendingMigrationTenantId)
                .Map(dest => dest.PendingMigrationProviderId, src => src.PendingMigrationProviderId)
                .Map(dest => dest.IsActive, src => src.IsActive)
                .Map(dest => dest.LockoutEnd, src => src.LockoutEnd != null && src.LockoutEnd > DateTimeOffset.UtcNow
                    ? Instant.FromDateTimeOffset(src.LockoutEnd.Value)
                    : (Instant?)null)
                .Map(dest => dest.LastActivityAt, src => src.LastActivityAt)
                .Map(dest => dest.Employee, src => src.Employee == null
                    ? null
                    : NavigationDto.Create(src.Employee.Id, src.Employee.Key, src.Employee.Name.DisplayName))
                .Map(dest => dest.Roles, src => src.UserRoles.Select(ur => new RoleListDto
                {
                    Id = ur.Role.Id,
                    Name = ur.Role.Name!,
                    Description = ur.Role.Description
                }).ToList());

        config
            .NewConfig<ApplicationRole, RoleListDto>()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Name, src => src.Name!)
                .Map(dest => dest.Description, src => src.Description);

        config
            .NewConfig<UserIdentity, UserIdentityDto>()
                .Map(dest => dest.Id, src => src.Id)
                .Map(dest => dest.Provider, src => src.Provider)
                .Map(dest => dest.ProviderTenantId, src => src.ProviderTenantId)
                .Map(dest => dest.ProviderSubject, src => src.ProviderSubject)
                .Map(dest => dest.IsActive, src => src.IsActive)
                .Map(dest => dest.LinkedAt, src => src.LinkedAt)
                .Map(dest => dest.UnlinkedAt, src => src.UnlinkedAt)
                .Map(dest => dest.UnlinkReason, src => src.UnlinkReason);
    }
}

