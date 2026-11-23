using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EnterpriseRag.Core.Domain.Observability;

/// <summary>
/// Instrumentación centralizada de trazas y métricas OpenTelemetry-compatibles
/// usando las APIs nativas de .NET (ActivitySource y Meter) sin cardinalidad alta ni datos sensibles.
/// </summary>
public static class RagTelemetry
{
    public const string ServiceName = "EnterpriseRag";

    public static readonly ActivitySource ActivitySource = new($"{ServiceName}.Pipeline", "1.0.0");
    public static readonly Meter Meter = new($"{ServiceName}.Metrics", "1.0.0");

    public static readonly Histogram<double> EmbeddingsDurationMs =
        Meter.CreateHistogram<double>("rag.embeddings.duration", unit: "ms", description: "Duración de generación de embeddings.");

    public static readonly Histogram<double> RetrievalDurationMs =
        Meter.CreateHistogram<double>("rag.retrieval.duration", unit: "ms", description: "Duración de recuperación vectorial.");

    public static readonly Histogram<double> GenerationDurationMs =
        Meter.CreateHistogram<double>("rag.generation.duration", unit: "ms", description: "Duración de generación de respuestas LLM.");

    public static readonly Histogram<double> QdrantDurationMs =
        Meter.CreateHistogram<double>("rag.qdrant.duration", unit: "ms", description: "Duración de operaciones contra Qdrant.");

    public static readonly Histogram<double> SqlDurationMs =
        Meter.CreateHistogram<double>("rag.sql.duration", unit: "ms", description: "Duración de consultas SQL Server.");

    public static readonly Counter<long> ExternalErrors =
        Meter.CreateCounter<long>("rag.external.errors", unit: "{error}", description: "Errores en dependencias externas.");
}
