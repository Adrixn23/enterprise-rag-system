# Arquitectura del Sistema — Enterprise RAG System

## 1. Principios Arquitectónicos

La solución sigue **Clean Architecture**, **Domain-Driven Design (DDD)** y **Result Pattern funcional** (`Result` / `Result<T>`), evitando el uso de excepciones para control de flujo de negocio:

- **Core.Domain:** Entidades (`BaseEntity`, `Document`, `DocumentChunk`), errores tipados por módulo (`AccountErrors`, `DocumentErrors`, `SearchErrors`, `ChatErrors`, `VectorStoreErrors`), enums y modelos de configuración (`JWTSettings`, `GeminiSettings`, `QdrantSettings`, `EmailSettings`). Sin dependencias de infraestructura.
- **Core.Application:** Servicios de orquestación (`DocumentService`, `SearchService`, `ChatService`), contratos (`IAccountService`, `IVectorStoreService`, `IEmbeddingService`, `IGenerativeAiService`, `IEmailSenderService`) y DTOs.
- **Infrastructure.Persistence:** `ApplicationDbContext` (EF Core 9 + SQL Server 2022), `DocumentRepository` y `DocumentChunkRepository`.
- **Infrastructure.Identity:** `IdentityContext`, `ApplicationUser` (con `TenantId`), `AccountService`, `EmailSettingsValidator`, `LoggingEmailSenderService` (Development) y `SmtpEmailSenderService` (Production).
- **Infrastructure.VectorStore:** `QdrantVectorStoreService` sobre `QdrantClient` (gRPC).
- **Infrastructure.ExternalServices:** `GeminiEmbeddingService`, `GeminiGenerativeAiService`, `LocalFileStorageService` y `TextExtractorService`.
- **IoC:** Registro modular de dependencias por capa (`AddApplicationDependencies`, `AddInfrastructurePersistenceDependencies`, `AddInfrastructureIdentityDependencies`, `AddVectorStoreDependencies`, `AddExternalServicesDependencies`).
- **Presentation.WebApi:** Controladores REST, `ResultExtensions` (mapeo a RFC 7807 `ProblemDetails`), `ExceptionHandlingMiddleware`, `UseForwardedHeaders` y `UseRateLimiter`.

## 2. Consistencia Distribuida y Compensación (Saga)

En `DocumentService.UploadDocumentAsync`:
1. Valida archivo (tamaño, Content-Type y *Magic Bytes*).
2. Guarda el archivo físico en `IFileStorageService`.
3. Extrae texto, genera chunks y solicita embeddings en lote (`IEmbeddingService`). Si falla, elimina el archivo físico.
4. Inserta puntos en Qdrant (`IVectorStoreService.UpsertPointAsync`). Si falla, elimina el archivo físico.
5. Persiste `Document` + `DocumentChunk` en SQL Server (`IDocumentRepository.AddAsync`). Si SQL lanza excepción, dispara compensación: elimina los puntos de Qdrant (`DeleteByDocumentAsync`) y borra el archivo físico.

En `DocumentService.DeleteDocumentAsync`:
1. Verifica pertenencia (`document.TenantId == tenantId`).
2. Elimina primero vectores en Qdrant; si falla, aborta antes de tocar SQL Server o el disco.
3. Marca chunks como eliminados (`IsDeleted = true`), elimina el archivo físico y elimina el registro de `Document`.

## 3. Propagación de Cancelación y Manejo de Errores

- Todos los endpoints y servicios propagan `CancellationToken`.
- `GeminiEmbeddingService` y `GeminiGenerativeAiService` diferencian una cancelación del cliente (`when (cancellationToken.IsCancellationRequested)` -> relanza `OperationCanceledException`) de un timeout interno del proveedor externo (devuelve `Result.Failure` semántico).
- `ExceptionHandlingMiddleware` intercepta `OperationCanceledException` cuando `context.RequestAborted.IsCancellationRequested`, registra a nivel `Information` y evita escribir sobre sockets ya cerrados comprobando `!context.Response.HasStarted`.
- `ResultExtensions` traduce códigos de dominio a HTTP `400`, `401`, `403`, `404`, `409`, `423`, `500` y `503` con formato `ProblemDetails`.

## 4. Ingestión Asíncrona, Outbox Pattern y Resiliencia

- **Ingestión asíncrona (`POST /api/v1/documents/upload` -> `202 Accepted`):** `DocumentService.EnqueueDocumentAsync` valida Magic Bytes, calcula SHA-256 por `TenantId`, guarda el archivo en `IFileStorageService` (`Local` o `S3`/MinIO mediante AWS SigV4) y persiste `Document (Pending)` + `OutboxMessage (IngestDocument)` dentro de la misma transacción SQL (`CreateExecutionStrategy().ExecuteAsync`).
- **Worker y Consistencia Eventual:** `DocumentIngestionWorker` (`BackgroundService`) reclama mensajes de `OutboxMessages` mediante lease atómico (`LockedUntil` + `ExecuteUpdateAsync`), ejecuta `ProcessPendingDocumentAsync` (batching Gemini de máx. 100 chunks por petición, Qdrant upsert y `CompleteIngestionAsync`) y aplica reintentos exponenciales o compensación `DeleteVectors` si falla SQL tras subir vectores. No existe atomicidad distribuida (2PC) entre SQL Server y Qdrant; la consistencia es eventual y recuperable tras reinicios.
- **Resiliencia y Observabilidad:** `Microsoft.Extensions.Http.Resilience` (`AddStandardResilienceHandler` con retry 429/5xx, `Retry-After`, backoff exponencial + jitter y circuit breaker), `EnableRetryOnFailure` en SQL Server, health checks `/health/live` y `/health/ready`, e instrumentación nativa `RagTelemetry` (`ActivitySource` + `Meter`) con propagación de `X-Correlation-Id`.
- **Clean Architecture pura en Application:** Se eliminó `Microsoft.AspNetCore.App` e `IFormFile` de `EnterpriseRag.Core.Application`, reemplazados por `Stream` + metadatos.
