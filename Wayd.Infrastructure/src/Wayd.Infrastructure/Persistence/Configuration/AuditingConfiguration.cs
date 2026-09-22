using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class AuditTrailConfig : IEntityTypeConfiguration<Trail>
{
    public void Configure(EntityTypeBuilder<Trail> builder)
    {
        builder.ToTable("AuditTrails", SchemaNames.Auditing);
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.PrimaryKey);

        // An identity, so the rows append. The application used to supply a Guid.CreateVersion7 through
        // BaseEntity: a v7 sorts by its leading bytes, but SQL Server orders uniqueidentifier by bytes
        // 10-15, which in a v7 are random, so every insert landed at a random point in the clustered index.
        // On the most written table in the application that is a page split every few rows — 99%
        // fragmentation and 25,071 pages within one import run, at roughly half density.
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Type).HasMaxLength(32).HasColumnType("varchar");
        builder.Property(x => x.SchemaName).HasMaxLength(64).HasColumnType("varchar");
        builder.Property(x => x.TableName).HasMaxLength(128).HasColumnType("varchar");
        builder.Property(x => x.DateTime).IsRequired();
        builder.Property(x => x.OldValues);
        builder.Property(x => x.NewValues);
        builder.Property(x => x.AffectedColumns);
        builder.Property(x => x.PrimaryKey).HasMaxLength(450);  // needs a max because of index
        builder.Property(x => x.CorrelationId).HasMaxLength(128);
    }
}