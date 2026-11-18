using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Links;

namespace Wayd.Infrastructure.Persistence.Configuration;

public class LinkConfiguration : IEntityTypeConfiguration<Link>
{
    public void Configure(EntityTypeBuilder<Link> builder)
    {
        builder.ToTable(name: "Links", SchemaNames.Links);

        builder.HasKey(o => o.Id);

        // Every read is one record's links. No foreign key: the owner is any module's record, held by type and id.
        builder.HasIndex(o => new { o.OwnerType, o.OwnerId });

        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.OwnerType).IsRequired().HasMaxLength(LinkOwnerTypeDescriptor.KeyMaxLength).IsUnicode(false);
        builder.Property(o => o.OwnerId).IsRequired();
        builder.Property(o => o.Name).IsRequired().HasMaxLength(Link.NameMaxLength);
        builder.Property(o => o.Url).IsRequired();
    }
}
