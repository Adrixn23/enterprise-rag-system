using EnterpriseRag.Core.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseRag.Core.Application.Contracts.GenericService
{
    public interface IGenericService<TResponseDto, TRequestDto, TKey>
         where TResponseDto : class
        where TRequestDto : class
    {
        Task<Result<TResponseDto>> AddAsync(TRequestDto dto, CancellationToken cancellationToken = default);
        Task<Result<bool>> UpdateAsync(TKey id, TRequestDto dto, CancellationToken cancellationToken = default);

        Task<Result<bool>> DeleteAsync(TKey id, CancellationToken cancellationToken = default);
        Task<Result<TResponseDto>> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

        Task<Result<IEnumerable<TResponseDto>>> GetAllAsync(CancellationToken cancellationToken = default);

    }
}
