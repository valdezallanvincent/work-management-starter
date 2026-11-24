using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Projects;
using Starter.Domain.Users;
using Starter.Domain.WorkItems;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.Title).HasMaxLength(WorkItem.TitleMaxLength).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(WorkItem.DescriptionMaxLength);
        builder.Property(w => w.RowVersion).IsRowVersion();

        // Stored as numbers, unlike other enums, because their order is meaningful: sorting by
        // priority or by workflow stage must not fall back to alphabetical order.
        builder.Property(w => w.Priority).HasConversion<int>().Metadata.SetMaxLength(null);
        builder.Property(w => w.Status).HasConversion<int>().Metadata.SetMaxLength(null);

        // Supports the default list (by project, filtered by status) and the per-project counts.
        builder.HasIndex(w => new { w.ProjectId, w.Status });
        // Supports "assigned to me" and the dashboard; Status is included so the index covers the filter.
        builder.HasIndex(w => new { w.AssigneeId, w.Status });
        builder.HasIndex(w => w.CompletedAtUtc).HasFilter("[CompletedAtUtc] IS NOT NULL");

        builder.HasOne<Project>().WithMany().HasForeignKey(w => w.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(w => w.ReporterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(w => w.AssigneeId).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureAudit();
        builder.Ignore(w => w.IsClosed);
    }
}
