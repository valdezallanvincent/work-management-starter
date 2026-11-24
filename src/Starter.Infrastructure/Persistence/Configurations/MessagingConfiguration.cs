using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Infrastructure.Messaging;

namespace Starter.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.CorrelationId).HasMaxLength(64).IsUnicode(false);
        builder.Property(m => m.LastError).HasMaxLength(2000);

        // The dispatcher only ever scans pending rows; the filter keeps the index small as history grows.
        builder.HasIndex(m => new { m.NextAttemptAtUtc, m.OccurredAtUtc })
            .HasFilter("[ProcessedAtUtc] IS NULL AND [DeadLetteredAtUtc] IS NULL")
            .HasDatabaseName("IX_OutboxMessages_Pending");
    }
}

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("ProcessedMessages");
        builder.HasKey(m => new { m.MessageId, m.Consumer });
        builder.Property(m => m.Consumer).HasMaxLength(100).IsUnicode(false);
    }
}
