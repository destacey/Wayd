using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Models.Scoring;
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
        builder.Property(d => d.OffersAllChannels).IsRequired();
        builder.Property(d => d.OffersAllTagCategories).IsRequired();

        builder.HasIndex(d => d.ScoringModelId);
        builder.HasOne<ScoringModel>()
            .WithMany()
            .HasForeignKey(d => d.ScoringModelId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        // The desk is saved whole; without this, two saves read at the same revision would both pass the
        // handler's check and the second would undo the first.
        builder.Property(d => d.Revision).IsConcurrencyToken();

        builder.HasMany(d => d.Members)
            .WithOne()
            .HasForeignKey(m => m.DeskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.Members).HasField("_members").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(d => d.Channels)
            .WithOne()
            .HasForeignKey(c => c.DeskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.Channels).HasField("_channels").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(d => d.TagCategories)
            .WithOne()
            .HasForeignKey(c => c.DeskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.TagCategories).HasField("_tagCategories").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class IntakeDeskTagCategoryConfiguration : IEntityTypeConfiguration<IntakeDeskTagCategory>
{
    public void Configure(EntityTypeBuilder<IntakeDeskTagCategory> builder)
    {
        builder.ToTable("IntakeDeskTagCategories", SchemaNames.DemandManagement);

        builder.HasKey(c => new { c.DeskId, c.CategoryId });

        builder.HasIndex(c => c.CategoryId);

        // Restrict: deleting an axis takes it off each desk that chose it through the desk, so the desk records
        // the change; a database cascade would remove it with no event.
        builder.HasOne<IntakeRequestTagCategory>()
            .WithMany()
            .HasForeignKey(c => c.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class IntakeDeskChannelConfiguration : IEntityTypeConfiguration<IntakeDeskChannel>
{
    public void Configure(EntityTypeBuilder<IntakeDeskChannel> builder)
    {
        builder.ToTable("IntakeDeskChannels", SchemaNames.DemandManagement);

        builder.HasKey(c => new { c.DeskId, c.ChannelId });

        builder.HasIndex(c => c.ChannelId);

        builder.HasOne<IntakeChannel>()
            .WithMany()
            .HasForeignKey(c => c.ChannelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class IntakeDeskMemberConfiguration : IEntityTypeConfiguration<IntakeDeskMember>
{
    public void Configure(EntityTypeBuilder<IntakeDeskMember> builder)
    {
        builder.ToTable("IntakeDeskMembers", SchemaNames.DemandManagement);

        builder.HasKey(m => new { m.DeskId, m.EmployeeId });

        // "Which desks is this employee on, and as what" — the question every authorization check asks.
        builder.HasIndex(m => m.EmployeeId);

        builder.Property(m => m.Role).IsRequired()
            .HasConversion<EnumConverter<IntakeDeskRole>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(m => m.EmployeeId)
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
        builder.HasIndex(r => r.AssigneeId);

        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Key).ValueGeneratedOnAdd();
        builder.Property(r => r.Title).IsRequired().HasMaxLength(256);
        builder.Property(r => r.Description).IsRequired();
        builder.Property(r => r.ExpectedBenefits);

        builder.Property(r => r.StatusId).IsRequired();
        builder.Property(r => r.StatusWorkflowId).IsRequired();
        // Indexed for the reassignment migrator, whose only query is "every record on this workflow".
        builder.HasIndex(r => r.StatusWorkflowId);
        builder.Property(r => r.StatusName).IsRequired().HasMaxLength(64);
        builder.Property(r => r.StatusCategory).IsRequired()
            .HasConversion<EnumConverter<StatusCategory>>()
            .HasColumnType("varchar")
            .HasMaxLength(32);
        builder.Property(r => r.StatusAliasValue).IsRequired();
        builder.Property(r => r.StatusTransitionCount).IsRequired();
        builder.Ignore(r => r.StatusAlias);
        builder.Ignore(r => r.HasLeftItsInitialStatus);
        builder.Ignore(r => r.IsOpen);
        builder.Property(r => r.StatusReason).HasMaxLength(1024);

        builder.ConfigureStatusHistory();

        // Restrict, so a request others give way to is never removed from under them; deleting one is refused
        // before it reaches the database.
        builder.HasIndex(r => r.DuplicateOfId);
        builder.HasOne<IntakeRequest>()
            .WithMany()
            .HasForeignKey(r => r.DuplicateOfId)
            .OnDelete(DeleteBehavior.Restrict);

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

        // Optional: a request submitted to a desk that offers no channel has none.
        builder.HasOne<IntakeChannel>()
            .WithMany()
            .HasForeignKey(r => r.ChannelId)
            .IsRequired(false)
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
            .HasForeignKey(r => r.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict);

        // An assignee is always a member of the request's desk. The handlers check it, but an assignment and a
        // member's removal from the desk can each pass their check and commit in either order; this key makes
        // whichever saves second fail instead of leaving a request owned by someone who no longer works its
        // desk. It is enforced only while AssigneeId is set.
        builder.HasOne<IntakeDeskMember>()
            .WithMany()
            .HasForeignKey(r => new { r.DeskId, r.AssigneeId })
            .HasPrincipalKey(m => new { m.DeskId, m.EmployeeId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Participants)
            .WithOne()
            .HasForeignKey(p => p.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Participants).HasField("_participants").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(r => r.Tags)
            .WithOne()
            .HasForeignKey(a => a.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Tags).HasField("_tags").UsePropertyAccessMode(PropertyAccessMode.Field);

        // A cache of the latest score for the list and board; IntakeRequestScores stays authoritative.
        builder.OwnsOne(r => r.CurrentScore, score =>
        {
            score.Property(s => s.Value).HasColumnName("CurrentScoreValue").HasColumnType("decimal(18,4)");
            score.Property(s => s.ScoredOn).HasColumnName("CurrentScoredOn");
            score.Property(s => s.ScoredById).HasColumnName("CurrentScoredById");
            score.Property(s => s.ScoringModelId).HasColumnName("CurrentScoringModelId");
            score.Property(s => s.ScoringModelName).HasColumnName("CurrentScoringModelName").HasMaxLength(128);

            score.HasOne<Employee>()
                .WithMany()
                .HasForeignKey(s => s.ScoredById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.HasMany(r => r.Scores)
            .WithOne()
            .HasForeignKey(s => s.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Scores).HasField("_scores").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class IntakeRequestScoreConfiguration : IEntityTypeConfiguration<IntakeRequestScore>
{
    public void Configure(EntityTypeBuilder<IntakeRequestScore> builder)
    {
        builder.ToTable("IntakeRequestScores", SchemaNames.DemandManagement);

        builder.HasKey(s => s.Id);

        // Two agents scoring at once compute the same next sequence; this makes the second save fail rather
        // than leave two scores claiming to be current.
        builder.HasIndex(s => new { s.RequestId, s.Sequence }).IsUnique();

        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.RequestId).IsRequired();

        // Not a foreign key: the snapshot stays valid whatever later happens to the model.
        builder.Property(s => s.ScoringModelId).IsRequired();
        builder.Property(s => s.ScoringModelKey).IsRequired();
        builder.Property(s => s.ScoringModelName).HasMaxLength(128).IsRequired();

        builder.Property(s => s.PrimaryValue).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(s => s.ScoredOn).IsRequired();
        builder.Property(s => s.ScoredById).IsRequired();
        builder.Property(s => s.Sequence).IsRequired();

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(s => s.ScoredById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Ratings)
            .WithOne()
            .HasForeignKey(r => r.ScoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Ratings).HasField("_ratings").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(s => s.Outputs)
            .WithOne()
            .HasForeignKey(o => o.ScoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Outputs).HasField("_outputs").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class IntakeRequestScoreRatingConfiguration : IEntityTypeConfiguration<IntakeRequestScoreRating>
{
    public void Configure(EntityTypeBuilder<IntakeRequestScoreRating> builder)
    {
        builder.ToTable("IntakeRequestScoreRatings", SchemaNames.DemandManagement);

        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.ScoreId);

        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.ScoreId).IsRequired();
        builder.Property(r => r.CriterionId).IsRequired();
        builder.Property(r => r.CriterionName).HasMaxLength(128).IsRequired();
        builder.Property(r => r.CriterionToken).HasColumnType("varchar").HasMaxLength(32).IsRequired();
        builder.Property(r => r.RatingValue).HasColumnType("decimal(9,4)").IsRequired();
        builder.Property(r => r.RatingLevelId);
        builder.Property(r => r.RatingLevelLabel).HasMaxLength(64);
        builder.Property(r => r.Order).IsRequired();
    }
}

public class IntakeRequestScoreOutputConfiguration : IEntityTypeConfiguration<IntakeRequestScoreOutput>
{
    public void Configure(EntityTypeBuilder<IntakeRequestScoreOutput> builder)
    {
        builder.ToTable("IntakeRequestScoreOutputs", SchemaNames.DemandManagement);

        builder.HasKey(o => o.Id);
        builder.HasIndex(o => o.ScoreId);

        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.ScoreId).IsRequired();
        builder.Property(o => o.Token).HasColumnType("varchar").HasMaxLength(32).IsRequired();
        builder.Property(o => o.Name).HasMaxLength(128).IsRequired();
        builder.Property(o => o.Value).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(o => o.IsPrimary).IsRequired();
        builder.Property(o => o.Order).IsRequired();
    }
}

public class IntakeRequestTagCategoryConfiguration : IEntityTypeConfiguration<IntakeRequestTagCategory>
{
    public void Configure(EntityTypeBuilder<IntakeRequestTagCategory> builder)
    {
        builder.ToTable("IntakeRequestTagCategories", SchemaNames.DemandManagement);

        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.Name).IsUnique();

        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Description).HasMaxLength(512);
        builder.Property(c => c.AllowsMany).IsRequired();
        builder.Property(c => c.Order).IsRequired();
        builder.Property(c => c.IsSystem).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();

        builder.HasMany(c => c.Tags)
            .WithOne()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Tags).HasField("_tags").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class IntakeRequestTagConfiguration : IEntityTypeConfiguration<IntakeRequestTag>
{
    public void Configure(EntityTypeBuilder<IntakeRequestTag> builder)
    {
        builder.ToTable("IntakeRequestTags", SchemaNames.DemandManagement);

        builder.HasKey(t => t.Id);
        builder.HasIndex(t => new { t.CategoryId, t.Name }).IsUnique();

        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Name).IsRequired().HasMaxLength(64);
        builder.Property(t => t.Description).HasMaxLength(512);
        builder.Property(t => t.IsActive).IsRequired();
    }
}

public class IntakeRequestTagAssignmentConfiguration : IEntityTypeConfiguration<IntakeRequestTagAssignment>
{
    public void Configure(EntityTypeBuilder<IntakeRequestTagAssignment> builder)
    {
        builder.ToTable("IntakeRequestTagAssignments", SchemaNames.DemandManagement);

        builder.HasKey(a => new { a.RequestId, a.TagId });

        // "Which requests carry this tag" and "is anything tagged along this axis": the tag filter, the usage
        // counts, and the in-use checks before a delete.
        builder.HasIndex(a => a.TagId);
        builder.HasIndex(a => a.CategoryId);

        // Restrict, so deleting a tag or an axis a request carries fails rather than silently untagging it; the
        // handlers refuse first.
        builder.HasOne<IntakeRequestTag>()
            .WithMany()
            .HasForeignKey(a => a.TagId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IntakeRequestTagCategory>()
            .WithMany()
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
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
