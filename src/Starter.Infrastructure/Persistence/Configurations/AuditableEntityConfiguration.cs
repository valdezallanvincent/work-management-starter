using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Starter.Domain.Common;

namespace Starter.Infrastructure.Persistence.Configurations;

internal static class AuditableEntityConfiguration
{
    public static void ConfigureAudit<T>(this EntityTypeBuilder<T> builder)
        where T : AuditableEntity
    {
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedById);
        builder.Property(e => e.ModifiedAtUtc);
        builder.Property(e => e.ModifiedById);
        builder.Ignore(e => e.DomainEvents);
    }
}
