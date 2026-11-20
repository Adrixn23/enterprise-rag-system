# Etapa 1: Base de compilación con .NET 9 SDK
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar archivos de proyectos para optimizar el caché de capas de Docker
COPY ["EnterpriseRag.slnx", "./"]
COPY ["src/Core/EnterpriseRag.Core.Domain/EnterpriseRag.Core.Domain.csproj", "src/Core/EnterpriseRag.Core.Domain/"]
COPY ["src/Core/EnterpriseRag.Core.Application/EnterpriseRag.Core.Application.csproj", "src/Core/EnterpriseRag.Core.Application/"]
COPY ["src/Infrastructure/EnterpriseRag.Infrastructure.Persistence/EnterpriseRag.Infrastructure.Persistence.csproj", "src/Infrastructure/EnterpriseRag.Infrastructure.Persistence/"]
COPY ["src/Infrastructure/EnterpriseRag.Infrastructure.Identity/EnterpriseRag.Infrastructure.Identity.csproj", "src/Infrastructure/EnterpriseRag.Infrastructure.Identity/"]
COPY ["src/Infrastructure/EnterpriseRag.Infrastructure.VectorStore/EnterpriseRag.Infrastructure.VectorStore.csproj", "src/Infrastructure/EnterpriseRag.Infrastructure.VectorStore/"]
COPY ["src/Infrastructure/EnterpriseRag.Infrastructure.ExternalServices/EnterpriseRag.Infrastructure.ExternalServices.csproj", "src/Infrastructure/EnterpriseRag.Infrastructure.ExternalServices/"]
COPY ["src/IoC/EnterpriseRag.IoC/EnterpriseRag.IoC.csproj", "src/IoC/EnterpriseRag.IoC/"]
COPY ["src/Presentation/EnterpriseRag.WebApi/EnterpriseRag.WebApi.csproj", "src/Presentation/EnterpriseRag.WebApi/"]
COPY ["tests/EnterpriseRag.UnitTests/EnterpriseRag.UnitTests.csproj", "tests/EnterpriseRag.UnitTests/"]

# Restaurar paquetes de la solución
RUN dotnet restore "src/Presentation/EnterpriseRag.WebApi/EnterpriseRag.WebApi.csproj"

# Copiar todo el código fuente
COPY . .

# Compilar y publicar WebApi en modo Release
WORKDIR "/src/src/Presentation/EnterpriseRag.WebApi"
RUN dotnet publish "EnterpriseRag.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Imagen final de ejecución ligera (ASP.NET Core Runtime 9.0)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Exponer puertos HTTP y HTTPS
EXPOSE 8080
EXPOSE 8081

# Copiar artefactos compilados desde la etapa de build
COPY --from=build /app/publish .

# Punto de entrada de la aplicación
ENTRYPOINT ["dotnet", "EnterpriseRag.WebApi.dll"]
