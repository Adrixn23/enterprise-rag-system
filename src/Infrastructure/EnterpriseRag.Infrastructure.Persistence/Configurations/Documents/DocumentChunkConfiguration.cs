namespace EnterpriseRag.Infrastructure.Persistence.Configurations.Documents;

using EnterpriseRag.Core.Domain.Entities.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("DocumentChunks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.DocumentId)
            .IsRequired();

        builder.Property(c => c.ChunkIndex)
            .IsRequired();

        builder.Property(c => c.Content)
            .IsRequired();

        builder.Property(c => c.TokenCount)
            .IsRequired();

        builder.Property(c => c.VectorId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.TenantId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.IsDeleted)
            .IsRequired();

        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => c.VectorId);
        builder.HasIndex(c => new { c.DocumentId, c.ChunkIndex });

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
