namespace EnterpriseRag.Core.Domain.Entities.Base;

public abstract class BaseEntity<TKey>
{
    // Identificador unico de la entidad (puede ser Guid, int, string, etc.)
    public TKey Id { get; set; } = default!;

    // Fecha y hora UTC en la que se creo el registro
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Fecha y hora UTC de la ultima modificacion (null si nunca se ha editado)
    public DateTimeOffset? UpdatedAt { get; set; }

    // Marca de borrado logico (Soft Delete) para no perder auditoria ni desincronizar Qdrant
    public bool IsDeleted { get; set; } = false;
}

public abstract class BaseEntity : BaseEntity<Guid>
{
}
