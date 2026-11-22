using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using EnterpriseRag.Core.Application.Contracts.FileService;
using EnterpriseRag.Core.Domain.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseRag.Infrastructure.ExternalServices.FileStorage;

/// <summary>
/// Proveedor de almacenamiento de objetos compatible con S3 / MinIO / Cloudflare R2
/// usando HTTP + AWS Signature V4 (HMAC-SHA256) para escalado horizontal multi-instancia.
/// </summary>
public class S3CompatibleFileStorageService : IFileStorageService
{
    private readonly HttpClient _httpClient;
    private readonly FileStorageSettings _settings;
    private readonly ILogger<S3CompatibleFileStorageService> _logger;

    public S3CompatibleFileStorageService(
        HttpClient httpClient,
        IOptions<FileStorageSettings> options,
        ILogger<S3CompatibleFileStorageService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
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

        var objectKey = $"{tenantId:N}/{Guid.NewGuid():N}_{safeFileName}";

        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, cancellationToken);
        var payloadBytes = ms.ToArray();

        using var request = CreateSignedRequest(HttpMethod.Put, objectKey, payloadBytes);
        request.Content = new ByteArrayContent(payloadBytes);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Archivo guardado en S3/MinIO bajo clave {ObjectKey} para Tenant {TenantId}", objectKey, tenantId);
        return objectKey;
    }

    public async Task<Stream?> GetFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return null;
        }

        using var request = CreateSignedRequest(HttpMethod.Get, storagePath, []);
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            return null;
        }

        response.EnsureSuccessStatusCode();
        var ms = new MemoryStream();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await stream.CopyToAsync(ms, cancellationToken);
        response.Dispose();
        ms.Position = 0;
        return ms;
    }

    public async Task<bool> DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return false;
        }

        using var request = CreateSignedRequest(HttpMethod.Delete, storagePath, []);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        return response.IsSuccessStatusCode;
    }

    internal HttpRequestMessage CreateSignedRequest(HttpMethod method, string objectKey, byte[] payload)
    {
        var baseUri = new Uri(_settings.ServiceUrl.TrimEnd('/') + "/");
        var normalizedKey = objectKey.TrimStart('/');
        var relativePath = $"{_settings.BucketName}/{normalizedKey}";
        var targetUri = new Uri(baseUri, relativePath);

        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var payloadHash = Convert.ToHexStringLower(SHA256.HashData(payload));

        var request = new HttpRequestMessage(method, targetUri);
        request.Headers.Host = targetUri.Authority;
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);

        var canonicalUri = targetUri.AbsolutePath;
        var canonicalHeaders = $"host:{targetUri.Authority}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        var canonicalRequest = $"{method.Method}\n{canonicalUri}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";

        var credentialScope = $"{dateStamp}/{_settings.Region}/s3/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";

        var signingKey = GetSignatureKey(_settings.SecretKey, dateStamp, _settings.Region, "s3");
        var signature = Convert.ToHexStringLower(HmacSha256(signingKey, stringToSign));

        var authHeader = $"AWS4-HMAC-SHA256 Credential={_settings.AccessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        request.Headers.TryAddWithoutValidation("Authorization", authHeader);

        return request;
    }

    private static byte[] GetSignatureKey(string secretKey, string dateStamp, string region, string service)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + secretKey), dateStamp);
        var kRegion = HmacSha256(kDate, region);
        var kService = HmacSha256(kRegion, service);
        return HmacSha256(kService, "aws4_request");
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }
}
