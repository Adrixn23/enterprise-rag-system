using EnterpriseRag.Core.Application.DTOs.Account;
using EnterpriseRag.Core.Domain.Common;

namespace EnterpriseRag.Core.Application.Contracts

{
    public  interface IAccountService
    {


        Task<Result<AuthenticationResponseDto>> AuthenticationAsync(LoginRequestDto request);
        Task<Result<string>> RegisterAsync(RegisterRequestDto request);

    }
}
