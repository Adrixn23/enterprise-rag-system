# Guía de Pruebas — Enterprise RAG System

## 1. Suite de Pruebas Unitarias (`tests/EnterpriseRag.UnitTests`)

Ejecución:
```powershell
dotnet test tests/EnterpriseRag.UnitTests/EnterpriseRag.UnitTests.csproj
```

Cobertura verificada (**103 pruebas unitarias**):
- **Aislamiento Multi-Tenant:**
  - `MultiTenantIsolationTests`: aislamiento en endpoints de documentos y ausencia de `TenantId` en `RegisterRequestDto`.
  - `ControllerTenantIsolationTests`: rechazo `401 Unauthorized` ante claim `"tenantId"` ausente, malformado o `Guid.Empty` en `ChatController`, `SearchController` y `DocumentsController`.
  - `DocumentServiceIsolationTests`: pruebas negativas de Tenant A intentando leer, listar chunks o eliminar documentos de Tenant B (`Times.Never` en SQL, Qdrant y almacenamiento).
  - `SearchAndChatIsolationTests`: rechazo de `Guid.Empty` y propagación exacta de `TenantId` hacia Qdrant y `IDocumentRepository`.
  - `QdrantVectorStoreIsolationTests`: rechazo de `Guid.Empty`, verificación de filtros `tenantId` / `documentId` y propagación de `OperationCanceledException`.
- **Embeddings, Resiliencia, Observabilidad, Paginación, Outbox y Storage:**
  - `GeminiEmbeddingServiceTests` y `GeminiGenerativeAiServiceTests`: batching en lotes ≤ 100 manteniendo orden, protección de `x-goog-api-key` fuera de URLs/logs, cancelación y timeout.
  - `HttpResiliencePolicyTests`: reintentos acotados ante `429` (`Retry-After`), `503` y `500` con `AddStandardResilienceHandler`.
  - `HealthChecksTests` y `RagTelemetryTests`: verificación de `/health/ready` sin exposición de secretos y métricas `RagTelemetry` sin alta cardinalidad.
  - `DocumentPaginationAndDeduplicationTests`, `OutboxAndAsyncIngestionTests`, `LocalFileStorageServiceTests` y `S3CompatibleFileStorageServiceTests`.
- **Identity, Confirmación y Rate Limiting:**
  - `EmailConfirmationAndEnvironmentTests` y `EmailSettingsValidationTests`: flujo Development vs Production, `ValidateOnStart()`, protección anti-enumeración y cooldown.
  - `IdentityTokenAndResendConcurrencyTests`: validación con `UserManager<ApplicationUser>` + `DataProtectorTokenProvider` reales (uso único, manipulación, expiración), 10 solicitudes concurrentes de reenvío y reintento tras fallo SMTP.
  - `AuthControllerTests` y `ConfirmEmailPagePlaywrightTests`: atributos `[EnableRateLimiting]`, cabeceras de seguridad, CSP con `nonce`, limpieza de `#token=` con `history.replaceState` y flujo completo en navegador headless con Playwright.
- **Consistencia, Chunks y Magic Bytes:**
  - `DocumentServiceConsistencyTests`, `TextExtractorMagicBytesTests`, `TextChunkerServiceTests`, `SearchServiceTests`, `ChatServiceTests`, `JWTSettingsValidationTests`.

### Nota sobre Playwright en CI / Linux
En Windows, `ConfirmEmailPagePlaywrightTests` utiliza Chromium o el canal `msedge` instalado. En entornos sin Edge/Chromium instalado, descarga el binario tras compilar con:
```powershell
pwsh tests/EnterpriseRag.UnitTests/bin/Debug/net9.0/playwright.ps1 install chromium
```

## 2. Suite de Pruebas de Integración (`tests/EnterpriseRag.IntegrationTests`)

Ejecución (requiere SQL Server 2022 y Qdrant corriendo):
```powershell
docker compose up -d sqlserver qdrant
dotnet test tests/EnterpriseRag.IntegrationTests/EnterpriseRag.IntegrationTests.csproj
```

Escenarios reales verificados (**6 pruebas**):
1. Búsqueda vectorial aislada para `Tenant A` en Qdrant.
2. Búsqueda vectorial aislada para `Tenant B` en Qdrant.
3. Intento de eliminación cruzada en Qdrant (`Tenant A` sobre documento de `Tenant B`).
4. Eliminación legítima en Qdrant sin afectar al otro tenant.
5. Consultas y soft-delete aislados en `DocumentRepository` y `DocumentChunkRepository` contra SQL Server.
6. Intento de `UpdateStatusAsync` cruzado en SQL Server sin alterar el documento ajeno.

Por seguridad, la suite exige que la base de datos sea exactamente `EnterpriseRagIntegrationTestsDb` y crea/elimina una colección temporal en Qdrant (`enterprise-it-{Guid}`), sin tocar `EnterpriseRagDb` ni `enterprise-documents`.

## 3. Validación Frontend (`client/`)

```powershell
cd client
npm run build   # Ejecuta tsc -b && vite build
npm run lint    # Ejecuta oxlint
```

## 4. Pruebas de Carga (`tests/load/k6-enterprise-rag.js`)

Suite k6 lista para ejecutar contra una instancia levantada (no ejecutada automáticamente en CI):
```powershell
k6 run -e BASE_URL=http://localhost:5235 -e TEST_EMAIL=demo@empresa.com -e TEST_PASSWORD=TuPassword k6-enterprise-rag.js
```
Mide throughput, P50/P95/P99 y tasa de error para login, listado paginado, búsqueda semántica, chat RAG y `/health/ready`.
