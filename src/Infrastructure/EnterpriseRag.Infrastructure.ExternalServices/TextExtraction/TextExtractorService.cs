namespace EnterpriseRag.Infrastructure.ExternalServices.TextExtraction;

using System.Text;
using EnterpriseRag.Core.Application.Contracts.TextExtraction;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

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
                var pageText = ExtractPageTextWithSpacing(page);
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    if (stringBuilder.Length > 0)
                    {
                        stringBuilder.AppendLine();
                    }
                    stringBuilder.Append(pageText);
                }
            }

            return stringBuilder.ToString();
        }
        finally
        {
            memoryStream?.Dispose();
        }
    }

    private static string ExtractPageTextWithSpacing(Page page)
    {
        var words = page.GetWords()
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .ToList();

        if (words.Count == 0)
        {
            return page.Text ?? string.Empty;
        }

        // Agrupar palabras por línea visual usando su coordenada vertical (Bottom) y ordenar de izquierda a derecha
        var sortedByY = words
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ToList();

        var lines = new List<List<Word>>();
        var currentLine = new List<Word> { sortedByY[0] };
        double currentBaseline = sortedByY[0].BoundingBox.Bottom;
        double currentHeight = sortedByY[0].BoundingBox.Height;

        for (int i = 1; i < sortedByY.Count; i++)
        {
            var word = sortedByY[i];
            double tolerance = Math.Max(2.0, Math.Max(currentHeight, word.BoundingBox.Height) * 0.45);

            if (Math.Abs(word.BoundingBox.Bottom - currentBaseline) <= tolerance)
            {
                currentLine.Add(word);
            }
            else
            {
                lines.Add(currentLine);
                currentLine = [word];
                currentBaseline = word.BoundingBox.Bottom;
                currentHeight = word.BoundingBox.Height;
            }
        }

        lines.Add(currentLine);

        var pageBuilder = new StringBuilder();
        foreach (var line in lines)
        {
            var lineText = string.Join(
                " ",
                line.OrderBy(w => w.BoundingBox.Left)
                    .Select(w => w.Text.Trim()));

            if (!string.IsNullOrWhiteSpace(lineText))
            {
                if (pageBuilder.Length > 0)
                {
                    pageBuilder.AppendLine();
                }
                pageBuilder.Append(lineText);
            }
        }

        return pageBuilder.ToString();
    }

    private static async Task<string> ExtractFromTextStreamAsync(Stream fileStream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
