# Seguridad y Aislamiento Multi-Tenant — Enterprise RAG System

## 1. Aislamiento Multi-Tenant

| Capa | Control Implementado |
|---|---|
| **Registro (`AccountService.RegisterAsync`)** | `RegisterRequestDto` no incluye `TenantId`. Cada registro público genera un `Guid.NewGuid()` nuevo en el servidor. |
| **JWT (`AccountService.GenerateJwtToken`)** | Emite claim `"tenantId"` firmado con HMAC-SHA256. Clave mínima de 32 bytes validada con `ValidateOnStart()`. `ClockSkew = TimeSpan.Zero`. |
| **Controladores (`Chat`, `Search`, `Documents`)** | `TryResolveTenantId` exige el claim `"tenantId"` válido y distinto de `Guid.Empty`; si falla, retorna `401 Unauthorized`. |
| **Servicios de Aplicación** | `ChatService`, `SearchService` y `DocumentService` rechazan `Guid.Empty` y validan `document.TenantId == tenantId` devolviendo `404 NotFound` ante accesos cruzados. |
| **Repositorios SQL Server** | `DocumentRepository` (`GetByTenantIdAsync`, `GetFileNamesByIdsAsync`, `UpdateStatusAsync`) y `DocumentChunkRepository` (`GetByDocumentIdAsync`, `DeleteByDocumentIdAsync`) exigen y filtran explícitamente por `TenantId` (y `!IsDeleted` en chunks). |
| **Vector DB (Qdrant)** | `QdrantVectorStoreService.SearchAsync` aplica `Conditions.MatchKeyword("tenantId", tenantId.ToString())` y `DeleteByDocumentAsync` filtra simultáneamente por `tenantId` y `documentId`. |

## 2. Confirmación de Correo Electrónico y Protección XSS

- **Separación por entorno (`IHostEnvironment`):**
  - En `Development`: autoconfirmación habilitada para pruebas locales y uso de `LoggingEmailSenderService` (que **nunca** registra el token ni el enlace en logs).
  - En `Production`: `EmailConfirmed = false` al registrar; se exige `SmtpEmailSenderService` y `EmailSettingsValidator` falla en el arranque (`ValidateOnStart()`) si falta `Host`, `FromEmail` válido o `PublicBaseUrl` HTTPS no-loopback.
- **Protección del token en URL:**
  - Los enlaces generados usan `{PublicBaseUrl}/api/v1/auth/confirm-email?userId={userId}#token={Uri.EscapeDataString(token)}`. El fragmento `#token=...` no viaja al servidor en la petición `GET`.
  - `GET /api/v1/auth/confirm-email` es seguro (sin mutación de estado), responde con `Cache-Control: no-store`, `Referrer-Policy: no-referrer`, `X-Content-Type-Options: nosniff` y `Content-Security-Policy` estricta con `nonce` criptográfico único por petición (sin `'unsafe-inline'`).
  - El script cliente extrae el token de `window.location.hash`, limpia inmediatamente la URL mediante `history.replaceState`, envía `POST /api/v1/auth/confirm-email` y renderiza mensajes únicamente con `textContent` (sin `innerHTML`).

## 3. Rate Limiting, Cooldown y Lockout

- **ASP.NET Core Identity Lockout:** 5 intentos fallidos de login bloquean la cuenta por 15 minutos.
- **Rate Limiting por IP (`Program.cs`):**
  - `auth-register`: 10 solicitudes / 15 min por IP.
  - `auth-resend`: 5 solicitudes / 15 min por IP.
  - `auth-confirm`: 10 solicitudes / 15 min por IP.
  - Rechazos retornan HTTP `429 Too Many Requests` con `ProblemDetails` y cabecera `Retry-After`.
- **Cooldown de reenvío (`AccountService.ResendConfirmationEmailAsync`):** 2 minutos por cuenta normalizada usando `IMemoryCache` y bloqueo particionado (`SemaphoreSlim`) para evitar condiciones de carrera TOCTOU. El cooldown solo se activa tras envío exitoso; las cuentas inexistentes o ya confirmadas devuelven la misma respuesta exitosa para prevenir enumeración.

## 4. Aislamiento de Chat en Frontend

- `RagChatWindow.tsx` aísla el historial por usuario bajo `rag_chat_history_${userId}`, sincroniza el estado al cambiar `userId` sin escrituras obsoletas y descarta respuestas en vuelo si el usuario cambia o cierra sesión. `useAuth.ts` limpia el historial del usuario al ejecutar `logout()`.
