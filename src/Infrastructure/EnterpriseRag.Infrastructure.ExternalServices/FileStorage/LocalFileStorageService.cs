namespace EnterpriseRag.Infrastructure.ExternalServices.FileStorage;

using EnterpriseRag.Core.Application.Contracts.FileService;
using Microsoft.Extensions.Configuration;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;

    public LocalFileStorageService(IConfiguration configuration)
    {
        var configuredPath = configuration["FileStorage:BasePath"];
        _basePath = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : Path.Combine(Directory.GetCurrentDirectory(), "Storage", "Uploads");
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var tenantDirectory = Path.Combine(_basePath, tenantId);
        Directory.CreateDirectory(tenantDirectory);

        var sanitizedFileName = Path.GetFileName(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
        var fullPath = Path.Combine(tenantDirectory, uniqueFileName);

        if (fileStream.CanSeek && fileStream.Position > 0)
        {
            fileStream.Position = 0;
        }

        await using var destinationStream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);

        await fileStream.CopyToAsync(destinationStream, cancellationToken);

        return fullPath;
    }

    public Task<bool> DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Task.FromResult(false);
        }

        if (File.Exists(storagePath))
        {
            File.Delete(storagePath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<Stream?> GetFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath) || !File.Exists(storagePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            storagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }
}
