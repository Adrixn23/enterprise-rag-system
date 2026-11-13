using EnterpriseRag.Core.Application.DTOs.Search;
using EnterpriseRag.Core.Domain.Common;

namespace EnterpriseRag.Core.Application.Contracts.Search;

public interface ISearchService
{
    Task<Result<List<SearchResultDto>>> SearchAsync(
        SearchQueryRequestDto request,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
