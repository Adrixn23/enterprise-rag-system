<div align="center">

# 🤖 Enterprise RAG System

### Plataforma Corporativa de Retrieval-Augmented Generation (RAG) & B2B AI Assistant
*Arquitectura Limpia · Aislamiento Multitenant · Búsqueda Semántica Vectorial · Web API en .NET 9 · SPA en React + TypeScript + Vite*


<br/>

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C# 13](https://img.shields.io/badge/C%23-13.0-239120?style=for-the-badge&logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![React](https://img.shields.io/badge/React-18/19-61DAFB?style=for-the-badge&logo=react&logoColor=black)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.0-3178C6?style=for-the-badge&logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![Vite](https://img.shields.io/badge/Vite-Bundler-646CFF?style=for-the-badge&logo=vite&logoColor=white)](https://vitejs.dev/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web%20API-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet)
[![Clean Architecture](https://img.shields.io/badge/Clean-Architecture-0078D4?style=for-the-badge&logo=blueprint&logoColor=white)](https://blog.cleancoder.com/)
[![EF Core](https://img.shields.io/badge/EF%20Core-Code%20First-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://learn.microsoft.com/ef/core/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC292B?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)
[![Qdrant](https://img.shields.io/badge/Qdrant-Vector%20DB-DC2626?style=for-the-badge&logo=qdrant&logoColor=white)](https://qdrant.tech/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white)](https://www.docker.com/)
[![JWT](https://img.shields.io/badge/JWT-Bearer%20Auth-000000?style=for-the-badge&logo=jsonwebtokens&logoColor=white)](https://jwt.io/)
[![Swagger](https://img.shields.io/badge/Swagger-OpenAPI%20Docs-85EA2D?style=for-the-badge&logo=swagger&logoColor=black)](https://swagger.io/)


</div>

---

## 📌 Descripción del Proyecto

**Enterprise RAG System** es una solución de backend corporativo de alto rendimiento diseñada para la ingesta, indexación, vectorización y consulta de documentos empresariales mediante técnicas avanzadas de **Retrieval-Augmented Generation (RAG)** e Inteligencia Artificial.

El sistema permite a múltiples organizaciones (*tenants*) consultar información de sus manuales, contratos, normativas y expedientes internos con respuestas precisas generadas por LLMs, garantizando **cero contaminación de datos entre empresas** y un estricto control de acceso basado en roles (RBAC).

---

## 🏛️ Arquitectura de la Solución (Clean Architecture)

El proyecto implementa los principios de **Clean Architecture** y **Domain-Driven Design (DDD)**, dividiendo las responsabilidades en capas independientes y desacopladas:

```
enterprise-rag-system/
├── src/
│   ├── Core/
│   │   ├── EnterpriseRag.Core.Domain/          # Núcleo del Negocio (Entidades, Result Pattern, Errores)
│   │   └── EnterpriseRag.Core.Application/     # Casos de Uso (DTOs, Interfaces de Servicios)
│   ├── Infrastructure/
│   │   ├── EnterpriseRag.Infrastructure.Identity/    # Autenticación, ASP.NET Identity, JWT, Migraciones
│   │   ├── EnterpriseRag.Infrastructure.Persistence/ # Acceso a Datos Relacionales (EF Core DbContext)
│   │   └── EnterpriseRag.Infrastructure.Shared/      # Ingesta, Embeddings, Integraciones Externas
│   ├── IoC/
│   │   └── EnterpriseRag.IoC/                  # Contenedor de Inversión de Control y Registro Modular
│   └── Presentation/
│       └── EnterpriseRag.WebApi/               # Composition Root, Middlewares, OpenAPI/Swagger
├── docker-compose.yml                          # Orquestación de SQL Server y Qdrant Vector DB
├── .env.example                                # Plantilla pública de variables de entorno
└── EnterpriseRag.slnx                          # Solución principal de .NET 9
```

### Detalle de Responsabilidades por Capa:

1. **`Core.Domain`**:
   * **Cero dependencias externas**: Modelo de dominio puro.
   * **Entidades Base**: `BaseEntity` con auditoría (`CreatedAt`, `LastModifiedAt`).
   * **Result Pattern Funcional**: Clases `Result` y `Result<T>` fuertemente tipadas con soporte para colecciones de `Error(Code, Description)`.
   * **Catálogo de Errores Tipados**: `AccountErrors` con códigos únicos para i18n y trazabilidad.
   * **Enums y Ajustes**: `Roles` (`SuperAdmin`, `Admin`, `Operador`) y `JWTSettings`.

2. **`Core.Application`**:
   * **DTOs de Entrada/Salida**: `LoginRequestDto`, `RegisterRequestDto`, `AuthenticationResponseDto`.
   * **Contratos de Servicios**: `IAccountService`, contratos de ingesta y repositorios.

3. **`Infrastructure.Identity`**:
   * **Entidad de Usuario Multitenant**: `ApplicationUser` heredando de `IdentityUser<Guid>` con `TenantId` y `FullName`.
   * **Contexto de Seguridad**: `IdentityContext` mapeado con Entity Framework Core.
   * **Servicio de Autenticación**: `AccountService` con hash criptográfico, asignación de roles y emisión de JWT.
   * **Generador JWT Criptográfico**: Firma HMAC-SHA256 con claims tipados (`Sub`, `Email`, `Jti`, `tenantId`, `Roles`).
   * **Semillas Iniciales (Seeds)**: Creación automática de roles (`DefaultRoles`) y superadministrador (`DefaultSuperAdmin`).
   * **Migraciones Versionadas**: Historial de migraciones de EF Core (`InitialIdentityMigration`).

4. **`Infrastructure.Persistence`**:
   * Contextos de base de datos relacionales y repositorios para la metadata de documentos y chunks.

5. **`Infrastructure.Shared`**:
   * Implementaciones de almacenamiento de archivos, algoritmos de fragmentación de texto (*chunking*), clientes de vectorización (OpenAI/Ollama) y base de datos vectorial (Qdrant).

6. **`IoC (Inversion of Control)`**:
   * Métodos de extensión modulares (`AddInfrastructureIdentityDependencies`, etc.) para desacoplar el registro de dependencias de la capa WebApi.

7. **`Presentation.WebApi`**:
   * Servidor HTTP ASP.NET Core en .NET 9.
   * Middlewares de seguridad: `app.UseAuthentication()` y `app.UseAuthorization()`.
   * Ejecución de Seeds al encender el servidor (`SeedIdentityDatabaseAsync`).
   * Documentación interactiva de APIs mediante OpenAPI/Swagger.

---

## 🚀 Guía de Puesta en Marcha Local

### Requisitos Previos
* [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) instalado.
* [Docker Desktop](https://www.docker.com/products/docker-desktop/) ejecutándose en Windows, Linux o macOS.
* Herramienta global de Entity Framework Core:
  ```powershell
  dotnet tool install --global dotnet-ef
  ```

---

### Paso 1: Clonar el Repositorio y Configurar Variables de Entorno

```powershell
git clone https://github.com/Adrixn23/enterprise-rag-system.git
cd enterprise-rag-system
```

Copia el archivo de plantilla `.env.example` para crear tu archivo `.env` local:
```powershell
Copy-Item .env.example .env
```

Define tu contraseña de SQL Server en el archivo `.env` local:
```env
MSSQL_SA_PASSWORD=TuPasswordSeguro@2026!
```

---

### Paso 2: Levantar Contenedores en Docker (SQL Server & Qdrant)

Inicia los motores de base de datos relacional y vectorial con volúmenes persistentes:

```powershell
docker compose up -d
```

Verifica que los contenedores estén en estado `Up`:
```powershell
docker ps
```
* **SQL Server**: `localhost:1433`
* **Qdrant Vector DB**: `localhost:6333` (Dashboard web en `http://localhost:6333/dashboard`)

---

### Paso 3: Configurar Secretos Locales con .NET User Secrets

Para mantener el repositorio seguro sin credenciales expuestas en Git, registra tus secretos locales:

```powershell
# 1. Inicializar User Secrets en la WebApi
dotnet user-secrets init --project src/Presentation/EnterpriseRag.WebApi

# 2. Configurar la cadena de conexión a SQL Server
dotnet user-secrets set "ConnectionStrings:IdentityConnection" "Server=localhost,1433;Database=EnterpriseRagIdentityDb;User Id=sa;Password=TuPasswordSeguro@2026!;TrustServerCertificate=True;MultipleActiveResultSets=true;" --project src/Presentation/EnterpriseRag.WebApi

# 3. Configurar los parámetros de JWT
dotnet user-secrets set "JWTSettings:Key" "EnterpriseRag_SuperSecret_Key_For_JWT_Authentication_2026_Development_Key_123456789!" --project src/Presentation/EnterpriseRag.WebApi
dotnet user-secrets set "JWTSettings:Issuer" "EnterpriseRagApi" --project src/Presentation/EnterpriseRag.WebApi
dotnet user-secrets set "JWTSettings:Audience" "EnterpriseRagClient" --project src/Presentation/EnterpriseRag.WebApi
dotnet user-secrets set "JWTSettings:DurationInMinutes" "60" --project src/Presentation/EnterpriseRag.WebApi
```

---

### Paso 4: Aplicar Migraciones de Base de Datos

Aplica las tablas de Identity a tu instancia de SQL Server en Docker:

```powershell
dotnet ef database update --project src/Infrastructure/EnterpriseRag.Infrastructure.Identity --startup-project src/Presentation/EnterpriseRag.WebApi --context IdentityContext
```

---

### Paso 5: Compilar y Ejecutar la API

```powershell
dotnet run --project src/Presentation/EnterpriseRag.WebApi
```

Abre tu navegador para acceder a la documentación interactiva:
* **Swagger / OpenAPI**: `https://localhost:7000/openapi/v1.json` o interfaz interactiva según configuración de desarrollo.

---

## 🔒 Decisiones Clave de Diseño y Seguridad

### 1. Aislamiento Multitenant Nativo (`TenantId`)
Cada usuario y documento posee un identificador global único `TenantId` (`Guid`). La información se filtra automáticamente a nivel de base de datos relacional y colecciones vectoriales en Qdrant, asegurando que ninguna empresa pueda consultar o indexar documentos de otra.

### 2. Result Pattern Funcional (Zero Exceptions for Business Flow)
Se erradicó el uso de excepciones (`throw new Exception()`) para errores previsibles (credenciales incorrectas, emails duplicados, validaciones de contraseña). Todas las operaciones retornan instancias de `Result<T>` con colecciones estructuradas de `Error`, garantizando un rendimiento óptimo y respuestas HTTP consistentes.

### 3. Sanitización de Repositorios (Zero Secrets in Git)
* **`appsettings.json`**: Mantenido como plantilla pública segura con valores en blanco `""`.
* **`docker-compose.yml`**: Contraseñas interpoladas con `${MSSQL_SA_PASSWORD}` leídas desde `.env`.
* **`.gitignore`**: Reglas estrictas para impedir la fuga de archivos `.env`, `.suo`, `.user` o binarios compilados.

---

## 👤 Autor

* **Adrián Francisco Brito Nelkitts** - *Backend & Cloud Software Engineer*
* GitHub: [@Adrixn23](https://github.com/Adrixn23)
* Repositorio: [Adrixn23/enterprise-rag-system](https://github.com/Adrixn23/enterprise-rag-system)

---

<div align="center">
Desarrollado con arquitectura empresarial limpia en <b>.NET 9</b> y <b>C# 13</b>.
</div>
