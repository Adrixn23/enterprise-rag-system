using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseRag.Core.Application.Contracts.TextExtraction
{
    public interface ITextExtractorService
    {
        Task<string> ExtractTextAsync(Stream fileStream, string contentType, CancellationToken cancellationToken = default);
        bool CanExtract(string contentType);
    }
}
