using EnterpriseRag.Core.Application.Contracts.FileService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EnterpriseRag.Infrastructure.ExternalServices.FileStorage;

/// <summary>
/// Implementación de almacenamiento en sistema de archivos local.
/// LIMITACIÓN CONOCIDA: El estado en disco local no se comparte entre réplicas ni sobrevive
/// a contenedores efímeros sin volumen persistente compartido. Para escalado horizontal
/// multi-instancia se requiere un proveedor distribuido (ej. S3 / MinIO / Azure Blob).
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;

        var configuredPath = configuration["FileStorage:BasePath"];
        var rawPath = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : Path.Combine(Directory.GetCurrentDirectory(), "Storage", "Uploads");

        _basePath = Path.GetFullPath(rawPath);

        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, Guid tenantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId no puede ser vacío.", nameof(tenantId));
        }

        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            safeFileName = "document.bin";
        }

        var tenantDirectory = Path.GetFullPath(Path.Combine(_basePath, tenantId.ToString("N")));
        EnsurePathWithinBase(tenantDirectory);

        if (!Directory.Exists(tenantDirectory))
        {
            Directory.CreateDirectory(tenantDirectory);
        }

        var uniqueFileName = $"{Guid.NewGuid():N}_{safeFileName}";
        var fullPath = Path.GetFullPath(Path.Combine(tenantDirectory, uniqueFileName));
        EnsurePathWithinBase(fullPath);

        await using var destinationStream = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous);

        await fileStream.CopyToAsync(destinationStream, cancellationToken);

        _logger.LogInformation("Archivo guardado localmente en {StoragePath} para Tenant {TenantId}", fullPath, tenantId);
        return fullPath;
    }

    public Task<Stream?> GetFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ResolveSafeExistingPath(storagePath);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream fileStream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult<Stream?>(fileStream);
    }

    public async Task<bool> DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ResolveSafeExistingPath(storagePath);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            await Task.Run(() => File.Delete(fullPath), cancellationToken);
            _logger.LogInformation("Archivo eliminado del almacenamiento local: {StoragePath}", fullPath);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el archivo local: {StoragePath}", fullPath);
            return false;
        }
    }

    private string? ResolveSafeExistingPath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(storagePath);
        EnsurePathWithinBase(fullPath);
        return fullPath;
    }

    private void EnsurePathWithinBase(string candidateFullPath)
    {
        var baseWithSeparator = _basePath.EndsWith(Path.DirectorySeparatorChar)
            ? _basePath
            : _basePath + Path.DirectorySeparatorChar;

        if (!candidateFullPath.StartsWith(baseWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidateFullPath, _basePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Acceso denegado: la ruta está fuera del directorio base de almacenamiento.");
        }
    }
}
