namespace EnterpriseRag.Infrastructure.ExternalServices.TextExtraction;

using System.Text;
using EnterpriseRag.Core.Application.Contracts.TextExtraction;
using UglyToad.PdfPig;

public class TextExtractorService : ITextExtractorService
{
    private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray(); // 0x25, 0x50, 0x44, 0x46, 0x2D
    private static readonly byte[] ExeMagicBytes = "MZ"u8.ToArray();   // 0x4D, 0x5A (Windows DOS MZ header)

    private static readonly HashSet<string> SupportedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "text/plain",
        "text/markdown",
        "text/csv",
        "text/html",
        "application/json",
        "application/xml",
        "text/xml"
    };

    public bool CanExtract(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var normalized = contentType.Split(';')[0].Trim();
        return SupportedContentTypes.Contains(normalized);
    }

    public bool IsValidFileContent(Stream fileStream, string contentType)
    {
        if (fileStream == null || fileStream.Length == 0 || !CanExtract(contentType))
        {
            return false;
        }

        if (!fileStream.CanSeek)
        {
            return true; // Si el stream no permite seek, delegamos al parser
        }

        var initialPosition = fileStream.Position;
        try
        {
            fileStream.Position = 0;
            Span<byte> header = stackalloc byte[16];
            int bytesRead = fileStream.Read(header);

            if (bytesRead < 4)
            {
                return false;
            }

            var normalized = contentType.Split(';')[0].Trim();

            // Bloquear ejecutables disfrazados
            if (header[0] == ExeMagicBytes[0] && header[1] == ExeMagicBytes[1])
            {
                return false;
            }

            // Validar firma PDF
            if (normalized.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return header.Slice(0, Math.Min(bytesRead, 5)).SequenceEqual(PdfMagicBytes);
            }

            // Validar archivos de texto plano: no deben contener bytes nulos binarios
            for (int i = 0; i < bytesRead; i++)
            {
                if (header[i] == 0x00)
                {
                    return false; // Archivo binario disfrazado de texto
                }
            }

            return true;
        }
        finally
        {
            fileStream.Position = initialPosition;
        }
    }

    public async Task<string> ExtractTextAsync(Stream fileStream, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        if (!CanExtract(contentType))
        {
            throw new NotSupportedException($"Content type '{contentType}' is not supported for text extraction.");
        }

        if (fileStream.CanSeek && fileStream.Position > 0)
        {
            fileStream.Position = 0;
        }

        var normalized = contentType.Split(';')[0].Trim();

        if (normalized.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return await ExtractFromPdfAsync(fileStream, cancellationToken);
        }

        return await ExtractFromTextStreamAsync(fileStream, cancellationToken);
    }

    private static async Task<string> ExtractFromPdfAsync(Stream fileStream, CancellationToken cancellationToken)
    {
        Stream seekableStream = fileStream;
        MemoryStream? memoryStream = null;

        try
        {
            if (!fileStream.CanSeek)
            {
                memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;
                seekableStream = memoryStream;
            }

            using var document = PdfDocument.Open(seekableStream);
            var stringBuilder = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(page.Text))
                {
                    stringBuilder.AppendLine(page.Text);
                }
            }

            return stringBuilder.ToString();
        }
        finally
        {
            memoryStream?.Dispose();
        }
    }

    private static async Task<string> ExtractFromTextStreamAsync(Stream fileStream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
