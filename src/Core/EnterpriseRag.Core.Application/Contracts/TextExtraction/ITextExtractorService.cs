namespace EnterpriseRag.Core.Application.Contracts.TextExtraction;

public interface ITextExtractorService
{
    Task<string> ExtractTextAsync(Stream fileStream, string contentType, CancellationToken cancellationToken = default);
    bool CanExtract(string contentType);
    bool IsValidFileContent(Stream fileStream, string contentType);
}
