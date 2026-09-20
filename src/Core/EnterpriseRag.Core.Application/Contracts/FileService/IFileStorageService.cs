using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseRag.Core.Application.Contracts.FileService
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(Stream fileStream, string fileName, string tenantId, CancellationToken cancellationToken = default);
        Task<bool> DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default);
        Task<Stream?> GetFileAsync(string storagePath, CancellationToken cancellationToken = default);
    }
}
