using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Notifications;
using Starter.Domain.Users;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Ignore(n => n.DomainEvents);

        builder.Property(n => n.Message).HasMaxLength(Notification.MessageMaxLength).IsRequired();
        builder.HasIndex(n => new { n.RecipientId, n.CreatedAtUtc });

        builder.HasOne<User>().WithMany().HasForeignKey(n => n.RecipientId).OnDelete(DeleteBehavior.Cascade);
    }
}
