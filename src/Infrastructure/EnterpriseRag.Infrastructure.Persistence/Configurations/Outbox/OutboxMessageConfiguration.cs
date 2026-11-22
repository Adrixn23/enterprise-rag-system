using EnterpriseRag.Core.Domain.Entities.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseRag.Infrastructure.Persistence.Configurations.Outbox;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.TenantId).IsRequired();
        builder.Property(m => m.AggregateId).IsRequired();

        builder.Property(m => m.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.PayloadJson)
            .IsRequired();

        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(m => m.LastError)
            .HasMaxLength(2000);

        builder.HasIndex(m => new { m.Status, m.NextAttemptAt });
        builder.HasIndex(m => new { m.TenantId, m.AggregateId });
    }
}
