using System.Text;
using EnterpriseRag.Core.Application.Contracts.Chat;
using EnterpriseRag.Core.Application.Contracts.GenerativeAi;
using EnterpriseRag.Core.Application.Contracts.Search;
using EnterpriseRag.Core.Application.DTOs.Chat;
using EnterpriseRag.Core.Application.DTOs.Search;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Chat;
using Microsoft.Extensions.Logging;

namespace EnterpriseRag.Core.Application.Services.Chat;

public class ChatService : IChatService
{
    private readonly ISearchService _searchService;
    private readonly IGenerativeAiService _generativeAiService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ISearchService searchService,
        IGenerativeAiService generativeAiService,
        ILogger<ChatService> logger)
    {
        _searchService = searchService;
        _generativeAiService = generativeAiService;
        _logger = logger;
    }

    public async Task<Result<ChatResponseDto>> AskAsync(
        ChatRequestDto request,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // 1. Validaciones defensivas de dominio
        if (tenantId == Guid.Empty)
        {
            return Result<ChatResponseDto>.Failure(ChatErrors.InvalidTenantId);
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Result<ChatResponseDto>.Failure(ChatErrors.EmptyMessage);
        }

        var trimmedMessage = request.Message.Trim();
        if (trimmedMessage.Length > 2000)
        {
            return Result<ChatResponseDto>.Failure(ChatErrors.MessageTooLong(2000));
        }

        // 2. Retrieval: Buscar los fragmentos mas relevantes en la base vectorial para este tenant
        var searchRequest = new SearchQueryRequestDto
        {
            Query = trimmedMessage,
            Limit = Math.Clamp(request.MaxContextChunks, 1, 10),
            MinScore = 0.4f // Umbral de similitud para asegurar contexto relevante
        };

        var searchResult = await _searchService.SearchAsync(searchRequest, tenantId, cancellationToken);
        var relevantChunks = (searchResult.IsSuccess && searchResult.Value is not null)
            ? searchResult.Value
            : [];

        // 3. Prompt Engineering: Construir System Instruction y Contexto Aumentado
        var systemInstruction = BuildSystemPrompt();
        var userPrompt = BuildUserPromptWithContext(trimmedMessage, relevantChunks);

        // 4. Generation: Sintesis de respuesta con Gemini LLM
        var generationResult = await _generativeAiService.GenerateResponseAsync(
            systemInstruction,
            userPrompt,
            cancellationToken);

        if (generationResult.IsFailure || generationResult.Value is null)
        {
            _logger.LogError("Fallo en la generacion de respuesta RAG para TenantId: {TenantId}", tenantId);
            return Result<ChatResponseDto>.Failure(generationResult.Errors.ToArray());
        }

        // 5. Retornar respuesta enriquecida con fuentes citables para auditoria
        var response = new ChatResponseDto
        {
            Answer = generationResult.Value,
            Sources = relevantChunks.Select(c => new ChatSourceDto
            {
                DocumentId = c.DocumentId,
                DocumentFileName = c.DocumentFileName ?? "Documento",
                ChunkIndex = c.ChunkIndex,
                Content = c.Content,
                RelevanceScore = c.Score
            }).ToList()
        };

        return Result<ChatResponseDto>.Success(response);
    }

    private static string BuildSystemPrompt()
    {
        return """
            Eres el asistente corporativo inteligente de Enterprise RAG System.
            Tu objetivo es responder a las preguntas de los usuarios basándote EXCLUSIVAMENTE en el contexto de documentos proporcionado.

            Reglas estrictas de comportamiento:
            1. Fundamenta tu respuesta solo en los fragmentos de documentos provistos en el contexto.
            2. Si la información no se encuentra en el contexto, indica amablemente: "No encuentro información suficiente en los documentos de la empresa para responder a tu pregunta."
            3. NO inventes hechos, políticas ni datos que no estén explícitamente en el contexto (Cero alucinaciones).
            4. Mantén un tono profesional, claro, conciso y corporativo.
            5. Si citas datos concretos, menciona el nombre del documento de donde proviene la información.
            """;
    }

    private static string BuildUserPromptWithContext(string userQuestion, List<SearchResultDto> chunks)
    {
        if (chunks.Count == 0)
        {
            return $"""
                [CONTEXTO DE DOCUMENTOS]
                No se encontraron documentos relacionados en la base de conocimientos.

                [PREGUNTA DEL USUARIO]
                {userQuestion}
                """;
        }

        var sb = new StringBuilder();
        sb.AppendLine("[CONTEXTO DE DOCUMENTOS DE LA EMPRESA]");
        sb.AppendLine("Usa los siguientes fragmentos para responder a la pregunta:");
        sb.AppendLine();

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            sb.AppendLine($"--- Fragmento {i + 1} (Documento: {chunk.DocumentFileName}, Similitud: {chunk.Score:P0}) ---");
            sb.AppendLine(chunk.Content);
            sb.AppendLine();
        }

        sb.AppendLine("[PREGUNTA DEL USUARIO]");
        sb.AppendLine(userQuestion);

        return sb.ToString();
    }
}
