using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Infrastructure.Persistence.Converters;
using Wayd.Organization.Domain.Enums;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class BaseTeamConfig : IEntityTypeConfiguration<BaseTeam>
{
    public void Configure(EntityTypeBuilder<BaseTeam> builder)
    {
        builder.ToTable("Teams", SchemaNames.Organization);

        builder.HasDiscriminator(c => c.Type)
            .HasValue<Team>(TeamType.Team)
            .HasValue<TeamOfTeams>(TeamType.TeamOfTeams);

        builder.HasKey(o => o.Id);
        builder.HasAlternateKey(o => o.Key);

        builder.HasIndex(o => new { o.Id, o.IsDeleted })
            .IncludeProperties(o => new { o.Key, o.Name, o.Code, o.IsActive })
            .WhereNotDeleted();
        builder.HasIndex(o => new { o.Key, o.IsDeleted })
            .IncludeProperties(o => new { o.Id, o.Name, o.Code, o.IsActive })
            .WhereNotDeleted();
        builder.HasIndex(o => o.Name)
            .IsUnique();
        builder.HasIndex(o => o.Code)
            .IsUnique()
            .IncludeProperties(o => new { o.Id, o.Key, o.Name, o.IsActive, o.IsDeleted });
        builder.HasIndex(o => new { o.IsActive, o.IsDeleted })
            .WhereNotDeleted();

        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Key).ValueGeneratedOnAdd();

        builder.Property(o => o.Name).IsRequired().HasMaxLength(128);
        builder.Property(o => o.Code).IsRequired()
            .HasConversion(
                o => o.Value,
                o => new TeamCode(o))
            .HasColumnType("varchar")
            .HasMaxLength(10);
        builder.Property(o => o.Description).HasMaxLength(1024);
        builder.Property(t => t.Type).IsRequired()
            .HasConversion<EnumConverter<TeamType>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(o => o.ActiveDate).IsRequired();
        builder.Property(o => o.InactiveDate);
        builder.Property(o => o.IsActive);

        // Soft Delete
        builder.Property(o => o.Deleted);
        builder.Property(o => o.DeletedBy);
        builder.Property(o => o.IsDeleted);

        // Relationships

        // Ignore
    }
}

public class TeamMembershipConfig : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.ToTable("TeamMemberships", SchemaNames.Organization);

        builder.HasKey(m => m.Id);

        builder.HasIndex(m => new { m.Id, m.IsDeleted })
            .IncludeProperties(m => new { m.SourceId, m.TargetId })
            .WhereNotDeleted();

        builder.HasIndex(m => new { m.SourceId, m.IsDeleted })
            .IncludeProperties(m => new { m.Id, m.TargetId })
            .WhereNotDeleted();

        builder.HasIndex(m => new { m.TargetId, m.IsDeleted })
            .IncludeProperties(m => new { m.Id, m.SourceId })
            .WhereNotDeleted();

        // Value Objects
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.ComplexProperty(m => m.DateRange, options =>
        {
            options.Property(d => d.Start).HasColumnName("Start").IsRequired();
            options.Property(d => d.End).HasColumnName("End");
        });

        // Relationships
        builder.HasOne(o => o.Source)
            .WithMany(m => m.ParentMemberships)
            .HasForeignKey(m => m.SourceId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(o => o.Target).WithMany(m => m.ChildMemberships).HasForeignKey(m => m.TargetId).OnDelete(DeleteBehavior.NoAction);

        // Soft Delete
        builder.Property(o => o.Deleted);
        builder.Property(o => o.DeletedBy);
        builder.Property(o => o.IsDeleted);
    }
}

public class TeamMemberRoleConfig : IEntityTypeConfiguration<TeamMemberRole>
{
    public void Configure(EntityTypeBuilder<TeamMemberRole> builder)
    {
        builder.ToTable("TeamMemberRoles", SchemaNames.Organization);

        builder.HasKey(r => r.Id);
        builder.HasAlternateKey(r => r.Key);

        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Key).ValueGeneratedOnAdd();

        builder.Property(r => r.Name).IsRequired().HasMaxLength(128);
        builder.HasIndex(r => r.Name).IsUnique().WhereNotDeleted();

        builder.Property(r => r.Description).HasMaxLength(1024);

        builder.Property(r => r.IsActive);

        // Soft Delete
        builder.Property(r => r.Deleted);
        builder.Property(r => r.DeletedBy);
        builder.Property(r => r.IsDeleted);
    }
}

public class TeamMemberConfig : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("TeamMembers", SchemaNames.Organization);

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.HasIndex(m => new { m.TeamId, m.EmployeeId, m.RoleId, m.IsDeleted })
            .IsUnique()
            .WhereNotDeleted()
            .HasDatabaseName("IX_TeamMembers_TeamId_EmployeeId_RoleId");

        builder.HasIndex(m => new { m.EmployeeId, m.IsDeleted })
            .WhereNotDeleted();

        builder.HasOne(m => m.Employee)
            .WithMany()
            .HasForeignKey(m => m.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(m => m.Team)
            .WithMany(t => t.Members)
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(m => m.Role)
            .WithMany()
            .HasForeignKey(m => m.RoleId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);

        // Soft Delete
        builder.Property(m => m.Deleted);
        builder.Property(m => m.DeletedBy);
        builder.Property(m => m.IsDeleted);
    }
}

public class TeamOperatingModelConfig : IEntityTypeConfiguration<TeamOperatingModel>
{
    public void Configure(EntityTypeBuilder<TeamOperatingModel> builder)
    {
        builder.ToTable("TeamOperatingModels", SchemaNames.Organization);

        builder.HasKey(m => m.Id);

        // Shadow property for the FK (not exposed on the domain entity)
        builder.Property<Guid>("TeamId");

        // Indexes using shadow property
        builder.HasIndex("TeamId");

        builder.HasIndex("TeamId")
            .IncludeProperties(m => new { m.Id, m.Methodology, m.SizingMethod })
            .WhereNull("End")
            .HasDatabaseName("IX_TeamOperatingModels_TeamId_Current");

        builder.Property(m => m.Id).ValueGeneratedNever();

        // Enums
        builder.Property(m => m.Methodology)
            .IsRequired()
            .HasConversion<EnumConverter<Methodology>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.Property(m => m.SizingMethod)
            .IsRequired()
            .HasConversion<EnumConverter<SizingMethod>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        // Value Object
        builder.ComplexProperty(m => m.DateRange, options =>
        {
            options.Property(d => d.Start).HasColumnName("Start").IsRequired();
            options.Property(d => d.End).HasColumnName("End");
        });

        // Relationships using shadow property FK (no navigation property on TeamOperatingModel)
        builder.HasOne<Team>()
            .WithMany(t => t.OperatingModels)
            .HasForeignKey("TeamId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}