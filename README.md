<div align="center">

# 🤖 Enterprise RAG System

### Plataforma Corporativa de Retrieval-Augmented Generation (RAG) & B2B AI Assistant
*Arquitectura Limpia · Aislamiento Multitenant · Búsqueda Semántica Vectorial · Web API en .NET 9 · SPA en React + TypeScript + Vite*

<br/>

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C# 13](https://img.shields.io/badge/C%23-13.0-239120?style=for-the-badge&logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Google Gemini](https://img.shields.io/badge/Google%20Gemini-API%20LLM-4285F4?style=for-the-badge&logo=google&logoColor=white)](https://ai.google.dev/)
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

**Enterprise RAG System** es una solución corporativa de alto rendimiento diseñada para la ingesta, indexación, vectorización y consulta de documentos empresariales mediante técnicas avanzadas de **Retrieval-Augmented Generation (RAG)** e Inteligencia Artificial impulsada por **Google Gemini API**.

El sistema permite a múltiples organizaciones (*tenants*) consultar información de sus manuales, contratos, normativas y expedientes internos con respuestas precisas, contextualizadas y fundamentadas, garantizando **cero contaminación de datos entre empresas** y un estricto control de acceso basado en roles (RBAC).

---

## 🏛️ Arquitectura de la Solución (Clean Architecture)

El proyecto implementa estrictamente los principios de **Clean Architecture** y **Domain-Driven Design (DDD)**, eliminando el antipatrón *Junk Drawer* y organizando la infraestructura en ensamblados con responsabilidad única:

```
enterprise-rag-system/
├── client/                                     # Frontend SPA en React + TypeScript + Vite [Fase UI / En desarrollo]
├── src/
│   ├── Core/
│   │   ├── EnterpriseRag.Core.Domain/          # Núcleo del Negocio (Entidades, Result Pattern, Catálogo de Errores)
│   │   └── EnterpriseRag.Core.Application/     # Casos de Uso (DTOs, Contratos e Interfaces de Servicios)
│   ├── Infrastructure/
│   │   ├── EnterpriseRag.Infrastructure.Persistence/      # EF Core, SQL Server 2022, Metadata Relacional
│   │   ├── EnterpriseRag.Infrastructure.Identity/         # ASP.NET Core Identity, JWT Criptográfico, TenantId
│   │   ├── EnterpriseRag.Infrastructure.VectorStore/      # Cliente Qdrant, Colecciones HNSW, Similitud Coseno
│   │   └── EnterpriseRag.Infrastructure.ExternalServices/ # Integración exclusiva con Google Gemini API
│   ├── IoC/
│   │   └── EnterpriseRag.IoC/                  # Inversión de Control y Registro Modular de Dependencias
│   └── Presentation/
│       └── EnterpriseRag.WebApi/               # Composition Root, Middlewares de Seguridad, OpenAPI/Swagger
├── docker-compose.yml                          # Orquestación de SQL Server y Qdrant Vector DB
├── .env.example                                # Plantilla pública de variables de entorno
└── EnterpriseRag.slnx                          # Solución principal de .NET 9
```

### Detalle de Responsabilidades por Capa:

1. **`Core.Domain`**:
   * **Cero dependencias externas**: Modelo de dominio puro.
   * **Entidades Base**: `BaseEntity` con auditoría (`CreatedAt`, `LastModifiedAt`).
   * **Result Pattern Funcional**: Clases `Result` y `Result<T>` fuertemente tipadas con colecciones de `Error(Code, Description)`.
   * **Catálogo de Errores Tipados**: `AccountErrors` con identificadores únicos para trazabilidad e i18n.
   * **Enums y Ajustes**: `Roles` (`SuperAdmin`, `Admin`, `Operador`) y `JWTSettings`.

2. **`Core.Application`**:
   * **DTOs de Entrada/Salida**: `LoginRequestDto`, `RegisterRequestDto`, `AuthenticationResponseDto`.
   * **Contratos de Servicios Desacoplados**: `IAccountService`, interfaces de repositorios y servicios de IA.

3. **`Infrastructure.Persistence`**:
   * **Persistencia Relacional**: Entity Framework Core con Microsoft SQL Server 2022.
   * **Almacenamiento de Metadata**: Mapeo relacional de documentos, chunks y estados de procesamiento.

4. **`Infrastructure.Identity`**:
   * **Entidad de Usuario Multitenant**: `ApplicationUser` heredando de `IdentityUser<Guid>` con `TenantId` y `FullName`.
   * **Contexto de Seguridad**: `IdentityContext` con migraciones versionadas.
   * **Servicio de Autenticación**: `AccountService` con hash PBKDF2, asignación de roles y emisión de JWT.
   * **Generador JWT**: Firma HMAC-SHA256 con claims tipados (`Sub`, `Email`, `Jti`, `tenantId`, `Roles`).
   * **Semillas Automáticas**: `DefaultRoles` y `DefaultSuperAdmin`.

5. **`Infrastructure.VectorStore`**:
   * **Base de Datos Vectorial**: Cliente nativo de **Qdrant**.
   * **Indexación Semántica**: Configuración de colecciones HNSW con métrica de distancia de coseno (*Cosine Similarity*).
   * **Filtrado por Tenant**: Aislamiento de vectores por `tenantId` en los payloads de Qdrant.

6. **`Infrastructure.ExternalServices`**:
   * **Motor de IA Exclusivo**: Integración directa con **Google Gemini API**.
   * **Embeddings Densos**: Generación de vectores semánticos de alta dimensionalidad (`text-embedding-004`).
   * **Generación Aumentada (LLM)**: Modelos generativos Gemini para síntesis de respuestas fundamentadas en contexto.

7. **`IoC (Inversion of Control)`**:
   * Métodos de extensión modulares:
     * `AddInfrastructurePersistenceDependencies`
     * `AddInfrastructureIdentityDependencies`
     * `AddVectorStoreDependencies`
     * `AddExternalServicesDependencies`

8. **`Presentation.WebApi`**:
   * Servidor HTTP ASP.NET Core en .NET 9.
   * Middlewares de seguridad: `app.UseAuthentication()` y `app.UseAuthorization()`.
   * Ejecución de Seeds al encender el servidor (`SeedIdentityDatabaseAsync`).
   * Documentación interactiva mediante OpenAPI/Swagger.

---

## 🚀 Guía de Puesta en Marcha Local

### Requisitos Previos
* [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) instalado.
* [Docker Desktop](https://www.docker.com/products/docker-desktop/) ejecutándose.
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

```powershell
docker compose up -d
```

Verifica el estado de los contenedores:
```powershell
docker ps
```
* **SQL Server**: `localhost:1433`
* **Qdrant Vector DB**: `localhost:6333` (Dashboard web en `http://localhost:6333/dashboard`)

---

### Paso 3: Configurar Secretos Locales con .NET User Secrets

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

```powershell
dotnet ef database update --project src/Infrastructure/EnterpriseRag.Infrastructure.Identity --startup-project src/Presentation/EnterpriseRag.WebApi --context IdentityContext
```

---

### Paso 5: Compilar y Ejecutar la API

```powershell
dotnet run --project src/Presentation/EnterpriseRag.WebApi
```

Documentación interactiva disponible en:
* **OpenAPI / Swagger**: `https://localhost:7000/openapi/v1.json`

---

## 🔒 Decisiones Clave de Diseño y Seguridad

### 1. Aislamiento Multitenant Nativo (`TenantId`)
Cada usuario, documento y vector en Qdrant posee un identificador global único `TenantId` (`Guid`). La información se aísla tanto en las tablas relacionales como en las consultas de similitud vectorial.

### 2. Result Pattern Funcional (Zero Exceptions for Business Flow)
Se erradicó el uso de excepciones (`throw new Exception()`) para flujos de negocio. Las operaciones retornan instancias de `Result<T>` con colecciones de `Error`, garantizando un rendimiento óptimo y respuestas HTTP consistentes.

### 3. Sanitización Total del Repositorio (Zero Secrets in Git)
* **`appsettings.json`**: Plantilla pública segura con valores en blanco `""`.
* **`docker-compose.yml`**: Contraseñas interpoladas con `${MSSQL_SA_PASSWORD}` leídas desde `.env`.
* **`.gitignore`**: Exclusión estricta de archivos `.env`, `.suo`, `.user` y binarios.

---

## 👤 Autor

* **Adrián Francisco Brito Nelkitts** - *Backend & Cloud Software Engineer*
* GitHub: [@Adrixn23](https://github.com/Adrixn23)
* Repositorio: [Adrixn23/enterprise-rag-system](https://github.com/Adrixn23/enterprise-rag-system)

---

<div align="center">
Desarrollado con arquitectura empresarial limpia en <b>.NET 9</b>, <b>C# 13</b> y <b>Google Gemini API</b>.
</div>
