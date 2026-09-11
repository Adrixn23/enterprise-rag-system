using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using EnterpriseRag.Core.Application.Contracts;
using EnterpriseRag.Core.Application.DTOs.Account;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.Identity.Entities;

namespace EnterpriseRag.Infrastructure.Identity.Services;

public class AccountService : IAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JWTSettings _jWTSettings;

    public Task<Result<AuthenticationResponseDto>> AuthenticationAsync(LoginRequestDto request)
    {
        throw new NotImplementedException();
    }

    public Task<Result<string>> RegisterAsync(RegisterRequestDto request)
    {
        throw new NotImplementedException();
    }
} 
