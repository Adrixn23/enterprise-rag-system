using EnterpriseRag.Core.Application.Contracts.GenericService;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Interfaces.GenericRepository;
using Mapster;

using EnterpriseRag.Core.Domain.Common.Errors;

namespace EnterpriseRag.Core.Application.Services.GenericServices;

public class GenericService<TEntity, TResponseDto, TRequestDto, TKey> : IGenericService<TResponseDto, TRequestDto, TKey>
    where TEntity : class
    where TResponseDto : class
    where TRequestDto : class
{
    private readonly IGenericRepository<TEntity, TKey> _repository;

    public GenericService(IGenericRepository<TEntity, TKey> repository)
    {
        _repository = repository;
    }

    public async Task<Result<TResponseDto>> AddAsync(TRequestDto dto, CancellationToken cancellationToken = default)
    {
        var entity = dto.Adapt<TEntity>();
        await _repository.AddAsync(entity, cancellationToken);

        var responseDTO = entity.Adapt<TResponseDto>();

        return Result<TResponseDto>.Success(responseDTO);

    }

    public async Task<Result<bool>> UpdateAsync(TKey id, TRequestDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return Result<bool>.Failure(Error.NotFound);

        }

        dto.Adapt(entity);

        await _repository.UpdateAsync(entity, cancellationToken);
        return Result<bool>.Success(true);


    }

    public async Task<Result<bool>> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return Result<bool>.Failure(Error.NotFound);
        }

        await _repository.DeleteAsync(entity, cancellationToken);
        return Result<bool>.Success(true);
    }

    public async Task<Result<TResponseDto>> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
      var entity =  await _repository.GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return Result<TResponseDto>.Failure(Error.NotFound);
        }

        var responseDto = entity.Adapt<TResponseDto>();
        return Result<TResponseDto>.Success(responseDto);

    }

    public async Task<Result<IEnumerable<TResponseDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
       var entity = await _repository.GetAllAsync(cancellationToken);
        var responsedto = entity.Adapt<IEnumerable<TResponseDto>>();
        return Result<IEnumerable<TResponseDto>>.Success(responsedto);
    }
}
