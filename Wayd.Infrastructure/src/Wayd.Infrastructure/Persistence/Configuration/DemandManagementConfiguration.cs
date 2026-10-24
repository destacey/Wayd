using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.Persistence.Converters;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class IntakeChannelConfiguration : IEntityTypeConfiguration<IntakeChannel>
{
    public void Configure(EntityTypeBuilder<IntakeChannel> builder)
    {
        builder.ToTable("IntakeChannels", SchemaNames.DemandManagement);

        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.Name).IsUnique();

        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Description).HasMaxLength(512);
        builder.Property(c => c.Order).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
    }
}

public class IntakeDeskConfiguration : IEntityTypeConfiguration<IntakeDesk>
{
    public void Configure(EntityTypeBuilder<IntakeDesk> builder)
    {
        builder.ToTable("IntakeDesks", SchemaNames.DemandManagement);

        builder.HasKey(d => d.Id);
        builder.HasAlternateKey(d => d.Key);
        builder.HasIndex(d => d.Name).IsUnique();

        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Key).ValueGeneratedOnAdd();
        builder.Property(d => d.Name).IsRequired().HasMaxLength(128);
        builder.Property(d => d.Description).IsRequired().HasMaxLength(1024);
        builder.Property(d => d.Visibility).IsRequired()
            .HasConversion<EnumConverter<IntakeDeskVisibility>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(d => d.IsRequesterSelectable).IsRequired();
        builder.Property(d => d.IsOpen).IsRequired();
        builder.Property(d => d.RequesterMayAddParticipants).IsRequired();

        builder.HasMany(d => d.Reviewers)
            .WithOne()
            .HasForeignKey(r => r.DeskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.Reviewers).HasField("_reviewers").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class IntakeDeskReviewerConfiguration : IEntityTypeConfiguration<IntakeDeskReviewer>
{
    public void Configure(EntityTypeBuilder<IntakeDeskReviewer> builder)
    {
        builder.ToTable("IntakeDeskReviewers", SchemaNames.DemandManagement);

        builder.HasKey(r => new { r.DeskId, r.EmployeeId });

        // "Which desks does this employee review on" — the question every Review check asks.
        builder.HasIndex(r => r.EmployeeId);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(r => r.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class IntakeRequestConfiguration : IEntityTypeConfiguration<IntakeRequest>
{
    public void Configure(EntityTypeBuilder<IntakeRequest> builder)
    {
        builder.ToTable("IntakeRequests", SchemaNames.DemandManagement);

        builder.HasKey(r => r.Id);
        builder.HasAlternateKey(r => r.Key);

        builder.HasIndex(r => r.DeskId);
        builder.HasIndex(r => r.RequesterId);
        builder.HasIndex(r => r.SubmittedById);
        builder.HasIndex(r => r.AssignedReviewerId);

        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Key).ValueGeneratedOnAdd();
        builder.Property(r => r.Title).IsRequired().HasMaxLength(256);
        builder.Property(r => r.Description).IsRequired();
        builder.Property(r => r.ExpectedBenefits);

        // Derived until requests follow their desk's review workflow, which persists a real status.
        builder.Ignore(r => r.StatusCategory);

        builder.OwnsOne(r => r.NeedBy, needBy =>
        {
            needBy.Property(n => n.Date).HasColumnName("NeedByDate");
            needBy.Property(n => n.Basis).HasColumnName("NeedByBasis")
                .HasConversion<EnumConverter<NeedByBasis>>()
                .HasColumnType("varchar")
                .HasMaxLength(32);
            needBy.Property(n => n.Reference).HasColumnName("NeedByReference").HasMaxLength(1024);
            needBy.Property(n => n.CommittedById).HasColumnName("NeedByCommittedById");
            needBy.Property(n => n.CommittedOn).HasColumnName("NeedByCommittedOn");

            needBy.HasOne<Employee>()
                .WithMany()
                .HasForeignKey(n => n.CommittedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.OwnsOne(r => r.Origin, origin =>
        {
            origin.Property(o => o.SourceSystem).HasColumnName("OriginSourceSystem").HasMaxLength(128);
            origin.Property(o => o.ExternalId).HasColumnName("OriginExternalId").HasMaxLength(256);
            origin.Property(o => o.Url).HasColumnName("OriginUrl").HasMaxLength(2048);

            // A source system and external id identify at most one request, so creating one from an upstream
            // system can be made idempotent without a lookup racing a second insert.
            origin.HasIndex(o => new { o.SourceSystem, o.ExternalId })
                .IsUnique()
                .HasFilter("[OriginSourceSystem] IS NOT NULL");
        });

        builder.HasOne<IntakeDesk>()
            .WithMany()
            .HasForeignKey(r => r.DeskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IntakeChannel>()
            .WithMany()
            .HasForeignKey(r => r.ChannelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(r => r.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(r => r.SubmittedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(r => r.AssignedReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Participants)
            .WithOne()
            .HasForeignKey(p => p.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Participants).HasField("_participants").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class IntakeRequestParticipantConfiguration : IEntityTypeConfiguration<IntakeRequestParticipant>
{
    public void Configure(EntityTypeBuilder<IntakeRequestParticipant> builder)
    {
        builder.ToTable("IntakeRequestParticipants", SchemaNames.DemandManagement);

        builder.HasKey(p => new { p.RequestId, p.EmployeeId });

        // "Which requests is this employee a participant on" — part of every read that decides substance.
        builder.HasIndex(p => p.EmployeeId);

        builder.Property(p => p.AddedOn).IsRequired();

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(p => p.AddedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
