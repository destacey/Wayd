using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Models;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class EmployeeConfig : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees", SchemaNames.Organization);

        builder.HasKey(e => e.Id);
        builder.HasAlternateKey(e => e.Key);

        builder.HasIndex(e => new { e.Id, e.IsDeleted })
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(e => e.EmployeeNumber)
            .IsUnique()
            .IncludeProperties(e => new { e.Id })
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(e => new { e.IsActive, e.IsDeleted })
            .HasFilter("[IsDeleted] = 0");

        // Email is one of the candidate keys for PeopleSync upserts (the email-matching path),
        // so it gets a unique filtered index — same shape as EmployeeNumber.
        builder.HasIndex(e => e.Email)
            .IsUnique()
            .IncludeProperties(e => new { e.Id })
            .HasFilter("[IsDeleted] = 0");

        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Key).ValueGeneratedOnAdd();

        builder.Property(e => e.EmployeeNumber).HasMaxLength(256).IsRequired();
        builder.Property(e => e.HireDate);

        builder.Property(e => e.Email)
            .HasConversion(
                e => e.Value,
                e => new EmailAddress(e))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.JobTitle).HasMaxLength(256);
        builder.Property(e => e.Department).HasMaxLength(256);
        builder.Property(e => e.OfficeLocation).HasMaxLength(256);
        builder.Property(e => e.IsActive);

        builder.Property(e => e.EmployeeType).HasMaxLength(128);

        // Value Objects
        builder.ComplexProperty(e => e.Name, options =>
        {
            options.Property(e => e.FirstName).HasColumnName("FirstName").HasMaxLength(100).IsRequired();
            options.Property(e => e.MiddleName).HasColumnName("MiddleName").HasMaxLength(100);
            options.Property(e => e.LastName).HasColumnName("LastName").HasMaxLength(100).IsRequired();
            options.Property(e => e.Suffix).HasColumnName("Suffix").HasMaxLength(50);
            options.Property(e => e.Title).HasColumnName("Title").HasMaxLength(50);
        });


        // Soft Delete
        builder.Property(e => e.Deleted);
        builder.Property(e => e.DeletedBy);
        builder.Property(e => e.IsDeleted);

        // Relationships
        builder.HasOne(e => e.Manager).WithMany(m => m.DirectReports).HasForeignKey(e => e.ManagerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(e => e.Emails).WithOne(e => e.Employee).HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Employee.Emails))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class EmployeeEmailConfig : IEntityTypeConfiguration<EmployeeEmail>
{
    public void Configure(EntityTypeBuilder<EmployeeEmail> builder)
    {
        builder.ToTable("EmployeeEmails", SchemaNames.Organization);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // One employee owns any given work address. Unfiltered (unlike the Employees indexes)
        // because these rows are hard-deleted on reconcile rather than soft-deleted.
        builder.HasIndex(e => e.Email).IsUnique();

        builder.HasIndex(e => e.EmployeeId);

        builder.Property(e => e.Email)
            .HasConversion(
                e => e.Value,
                e => new EmailAddress(e))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.IsPrimary).IsRequired();
    }
}

public class ExternalEmployeeBlacklistItemConfig : IEntityTypeConfiguration<ExternalEmployeeBlacklistItem>
{
    public void Configure(EntityTypeBuilder<ExternalEmployeeBlacklistItem> builder)
    {
        builder.ToTable("ExternalEmployeeBlacklistItems", SchemaNames.Organization);

        builder.HasKey(e => e.ObjectId);
        builder.HasIndex(e => e.ObjectId);
    }
}