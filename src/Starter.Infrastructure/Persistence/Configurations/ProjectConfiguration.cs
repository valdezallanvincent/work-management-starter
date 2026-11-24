using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Projects;
using Starter.Domain.Users;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Key)
            .HasConversion(key => key.Value, value => ProjectKey.Create(value))
            .HasMaxLength(ProjectKey.MaxLength)
            .IsUnicode(false)
            .IsRequired();
        builder.HasIndex(p => p.Key).IsUnique();

        builder.Property(p => p.Name).HasMaxLength(Project.NameMaxLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(Project.DescriptionMaxLength);
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.HasIndex(p => new { p.Status, p.Name });
        builder.HasIndex(p => p.OwnerId);

        builder.HasOne<User>().WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);

        builder.ConfigureAudit();
        builder.Ignore(p => p.IsArchived);
    }
}
