using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Imports;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class ImportProcessConfig : IEntityTypeConfiguration<ImportProcess>
{
    public void Configure(EntityTypeBuilder<ImportProcess> builder)
    {
        builder.ToTable("ImportProcesses", SchemaNames.Imports);

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.ImportType).HasMaxLength(64).IsRequired();
        builder.Property(p => p.SubmittedByUserId).HasMaxLength(128).IsRequired();
        // A concurrency token: the runner, a person stopping or resuming the run, and the stall sweep all
        // write it from different requests. Without the check the last write wins — a worker releasing a
        // failed attempt would overwrite a stop, and two deliveries could both claim the same run.
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(32).IsRequired().IsConcurrencyToken();
        builder.Property(p => p.Error).HasMaxLength(2048);
        builder.Property(p => p.LastAttemptCorrelationId).HasMaxLength(128);

        // Drives the stall sweep: Processing runs whose heartbeat has gone quiet.
        builder.HasIndex(p => new { p.Status, p.LastProgressOn });

        // Drives the retention sweep, and the Settings list's default "most recent first" ordering.
        builder.HasIndex(p => new { p.Status, p.CompletedOn });

        builder.HasIndex(p => p.SubmittedOn);

        // A seed run submits many files under one group; the list collapses them into one entry.
        builder.HasIndex(p => p.SubmissionGroupId).HasFilter("[SubmissionGroupId] IS NOT NULL");

        builder.HasMany(p => p.Rows)
            .WithOne()
            .HasForeignKey(r => r.ImportProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(ImportProcess.Rows))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class ImportProcessRowConfig : IEntityTypeConfiguration<ImportProcessRow>
{
    public void Configure(EntityTypeBuilder<ImportProcessRow> builder)
    {
        builder.ToTable("ImportProcessRows", SchemaNames.Imports);

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.ImportId).HasMaxLength(ImportProcessRow.MaxImportIdLength).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(r => r.Error).HasMaxLength(ImportProcessRow.MaxMessageLength);
        builder.Property(r => r.Warning).HasMaxLength(ImportProcessRow.MaxMessageLength);

        // A row's parsed JSON has no useful bound, and it is never queried by value.
        builder.Property(r => r.Payload);

        // Enforces the contract that an importId identifies one row within its file, so a caller can rely on
        // the returned mapping being unambiguous. The structural validation rejects duplicates first; this
        // stops anything that slips past it from being persisted.
        builder.HasIndex(r => new { r.ImportProcessId, r.ImportId }).IsUnique();

        // The runner claims Pending rows for a process; retry and resume filter the same index by status.
        builder.HasIndex(r => new { r.ImportProcessId, r.Status });
    }
}
