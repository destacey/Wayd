using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Employees;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class ApplicationUserConfig : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users", SchemaNames.Identity);

        builder.HasIndex(u => u.Id)
            .IncludeProperties(e => new { e.UserName, e.EmployeeId, e.Email, e.FirstName, e.LastName });

        builder.Property(u => u.FirstName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);
        builder.Property(u => u.PhoneNumber).HasMaxLength(20);

        builder.Property(u => u.LastActivityAt);

        builder.Property(u => u.LoginProvider).HasMaxLength(50).IsRequired();

        builder.Property(u => u.PendingMigrationTenantId).HasMaxLength(100);

        builder.Property(u => u.PendingMigrationStagedAt);

        builder.Property(u => u.PendingMigrationProviderId).HasMaxLength(50);

        builder.Property(u => u.MustChangePassword).HasDefaultValue(false);

        builder.Property(u => u.RefreshToken).HasMaxLength(256);
        builder.Property(u => u.RefreshTokenExpiryTime);

        builder.OwnsOne(u => u.Preferences, prefs =>
        {
            prefs.ToJson();
            prefs.Property(p => p.Tours)
                 .HasConversion(
                     v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                     v => JsonSerializer.Deserialize<Dictionary<string, bool>>(v, JsonSerializerOptions.Default)
                           ?? new Dictionary<string, bool>()
                 );
            prefs.Property(p => p.ThemeConfig)
                 .HasConversion(
                     v => v == null ? null : JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                     v => v == null ? null : JsonSerializer.Deserialize<UserThemeConfig>(v, JsonSerializerOptions.Default)
                 );
        });

        // Relationships
        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.NoAction);

        // Configure many-to-many relationship with roles
        builder.HasMany(u => u.UserRoles)
            .WithOne()
            .HasForeignKey(ur => ur.UserId)
            .IsRequired();
    }
}

public class UserIdentityConfig : IEntityTypeConfiguration<UserIdentity>
{
    public void Configure(EntityTypeBuilder<UserIdentity> builder)
    {
        builder.ToTable("UserIdentities", SchemaNames.Identity);

        builder.HasKey(ui => ui.Id);
        builder.Property(ui => ui.Id).ValueGeneratedNever();

        builder.Property(ui => ui.UserId).HasMaxLength(450).IsRequired();
        builder.Property(ui => ui.Provider).HasMaxLength(50).IsRequired();
        builder.Property(ui => ui.ProviderTenantId).HasMaxLength(100);
        builder.Property(ui => ui.ProviderSubject).HasMaxLength(256).IsRequired();
        builder.Property(ui => ui.IsActive).IsRequired();
        builder.Property(ui => ui.LinkedAt).IsRequired();
        builder.Property(ui => ui.UnlinkedAt);
        builder.Property(ui => ui.UnlinkReason).HasMaxLength(50);

        // One active identity per (provider, tenant, subject). Filtered so inactive
        // rows don't collide and so NULL tenants (Wayd provider) are handled per
        // SQL Server filtered-unique-index semantics.
        builder.HasIndex(ui => new { ui.Provider, ui.ProviderTenantId, ui.ProviderSubject })
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("UX_UserIdentities_Provider_Tenant_Subject_Active");

        builder.HasIndex(ui => ui.UserId)
            .HasDatabaseName("IX_UserIdentities_UserId");

        builder.HasOne(ui => ui.User)
            .WithMany(u => u.Identities)
            .HasForeignKey(ui => ui.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ApplicationRoleConfig : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("Roles", SchemaNames.Identity);
    }
}

public class ApplicationRoleClaimConfig : IEntityTypeConfiguration<ApplicationRoleClaim>
{
    public void Configure(EntityTypeBuilder<ApplicationRoleClaim> builder) =>
        builder
            .ToTable("RoleClaims", SchemaNames.Identity);
}

public class ApplicationUserRoleConfig : IEntityTypeConfiguration<ApplicationUserRole>
{
    public void Configure(EntityTypeBuilder<ApplicationUserRole> builder)
    {
        builder.ToTable("UserRoles", SchemaNames.Identity);

        // Configure navigation properties
        builder.HasOne<ApplicationUser>()
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .IsRequired();

        builder.HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .IsRequired();
    }
}

public class IdentityUserClaimConfig : IEntityTypeConfiguration<IdentityUserClaim<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<string>> builder) =>
        builder
            .ToTable("UserClaims", SchemaNames.Identity);
}

public class IdentityUserLoginConfig : IEntityTypeConfiguration<IdentityUserLogin<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<string>> builder) =>
        builder
            .ToTable("UserLogins", SchemaNames.Identity);
}

public class IdentityUserTokenConfig : IEntityTypeConfiguration<IdentityUserToken<string>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<string>> builder) =>
        builder
            .ToTable("UserTokens", SchemaNames.Identity);
}

public class WaydUserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToView("vw_WaydUsers", SchemaNames.Identity);

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasMaxLength(450);
        builder.Property(u => u.UserName).HasMaxLength(256);
        builder.Property(u => u.FirstName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);
        builder.Property(u => u.DisplayName).HasMaxLength(200);
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.IsActive);
    }
}

public class OidcProviderConfig : IEntityTypeConfiguration<OidcProvider>
{
    public void Configure(EntityTypeBuilder<OidcProvider> builder)
    {
        builder.ToTable("OidcProviders", SchemaNames.Identity);

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name)
            .HasMaxLength(50)
            .IsRequired();

        // Stable provider key — must match the Provider column on UserIdentity. A
        // duplicate would let two configurations claim the same identity rows.
        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasDatabaseName("UX_OidcProviders_Name");

        builder.Property(p => p.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.ProviderType)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(p => p.Authority)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.ClientId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Audience)
            .HasMaxLength(500)
            .IsRequired();

        // Scopes and AllowedTenantIds are small string lists. JSON columns keep the
        // schema flat (no child tables), match the round-trip shape the domain expects,
        // and are queryable via OPENJSON when needed.
        builder.Property(p => p.Scopes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>())
            .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyList<string>>(
                (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
                v => v == null ? 0 : v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s)),
                v => v.ToList()));
        builder.Property(p => p.Scopes).HasMaxLength(2000);

        builder.Property(p => p.AllowedTenantIds)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default))
            .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyList<string>?>(
                (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
                v => v == null ? 0 : v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s)),
                v => v == null ? null : v.ToList()));
        builder.Property(p => p.AllowedTenantIds).HasMaxLength(4000);

        builder.Property(p => p.ClockSkewSeconds)
            .IsRequired()
            .HasDefaultValue(60);

        builder.Property(p => p.IsEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.OwnsOne(p => p.RegistrationPolicy, policy =>
        {
            policy.Property(rp => rp.AllowAutoRegistration)
                .HasColumnName("AllowAutoRegistration")
                .IsRequired()
                .HasDefaultValue(false);

            policy.Property(rp => rp.RequireEmployeeRecord)
                .HasColumnName("RequireEmployeeRecord");

            policy.Property(rp => rp.DefaultRoleId)
                .HasColumnName("DefaultRoleId")
                .HasMaxLength(450);

            // Real FK to the role, with NO ACTION on delete: a role that's pinned as
            // a provider's auto-registration default cannot be deleted until the
            // provider is repointed. This is the hard guarantee that the stored
            // DefaultRoleId can never dangle; RoleService.Delete surfaces the same
            // rule as a friendly ConflictException before the constraint fires.
            policy.HasOne<ApplicationRole>()
                .WithMany()
                .HasForeignKey(rp => rp.DefaultRoleId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Navigation(p => p.RegistrationPolicy).IsRequired();
    }
}

public class PersonalAccessTokenConfig : IEntityTypeConfiguration<PersonalAccessToken>
{
    public void Configure(EntityTypeBuilder<PersonalAccessToken> builder)
    {
        builder.ToTable("PersonalAccessTokens", SchemaNames.Identity);

        builder.HasKey(p => p.Id);

        // Indexes
        builder.HasIndex(p => p.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_PersonalAccessTokens_TokenHash");

        builder.HasIndex(p => p.UserId)
            .HasDatabaseName("IX_PersonalAccessTokens_UserId");

        builder.HasIndex(p => p.ExpiresAt)
            .HasDatabaseName("IX_PersonalAccessTokens_ExpiresAt");

        builder.HasIndex(p => new { p.UserId, p.RevokedAt })
            .HasDatabaseName("IX_PersonalAccessTokens_UserId_RevokedAt");

        // Composite index for efficient token lookup
        builder.HasIndex(p => new { p.TokenIdentifier, p.RevokedAt, p.ExpiresAt })
            .HasDatabaseName("IX_PersonalAccessTokens_TokenIdentifier_RevokedAt_ExpiresAt");

        // Properties
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.TokenIdentifier)
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(p => p.TokenHash)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(p => p.EmployeeId);

        builder.Property(p => p.Scopes)
            .HasMaxLength(4000); // Allows for JSON array of permission names

        builder.Property(p => p.ExpiresAt)
            .IsRequired();

        builder.Property(p => p.LastUsedAt);

        builder.Property(p => p.RevokedAt);

        builder.Property(p => p.RevokedBy).HasMaxLength(450);

        // Relationships
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}