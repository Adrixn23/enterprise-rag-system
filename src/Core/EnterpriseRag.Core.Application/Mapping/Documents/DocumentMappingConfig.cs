using EnterpriseRag.Core.Application.DTOs.Documents;
using EnterpriseRag.Core.Domain.Entities.Documents;
using Mapster;

namespace EnterpriseRag.Core.Application.Mapping.Documents;

public class DocumentMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Document, DocumentResponseDto>()
            .Map(dest => dest.TotalChunks, src => src.Chunks != null ? src.Chunks.Count : 0);

        config.NewConfig<DocumentChunk, DocumentChunkResponseDto>();
    }
}
