using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Users;
using Starter.Domain.WorkItems;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class WorkItemCommentConfiguration : IEntityTypeConfiguration<WorkItemComment>
{
    public void Configure(EntityTypeBuilder<WorkItemComment> builder)
    {
        builder.ToTable("WorkItemComments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Body).HasMaxLength(WorkItemComment.BodyMaxLength).IsRequired();

        // Version 7 GUIDs are time ordered, but listing by CreatedAtUtc keeps the intent explicit.
        builder.HasIndex(c => new { c.WorkItemId, c.CreatedAtUtc });

        builder.HasOne<WorkItem>().WithMany().HasForeignKey(c => c.WorkItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureAudit();
    }
}
