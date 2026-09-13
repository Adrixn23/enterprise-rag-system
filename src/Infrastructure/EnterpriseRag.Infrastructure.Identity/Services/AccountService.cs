using EnterpriseRag.Core.Application.Contracts;
using EnterpriseRag.Core.Application.DTOs.Account;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;


namespace EnterpriseRag.Infrastructure.Identity.Services;

public class AccountService : IAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JWTSettings _jwtSettings;

    public AccountService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOptions<JWTSettings> jwtSettings)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<Result<string>> RegisterAsync(RegisterRequestDto request)
    {
        var userWithSameEmail = await _userManager.FindByEmailAsync(request.Email);
        if (userWithSameEmail != null)
        {
            return Result<string>.Failure(AccountErrors.EmailAlreadyExists);
        }
        var user = new ApplicationUser
        {

            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            TenantId = request.TenantId,
            EmailConfirmed = true

        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => new Error(e.Code, e.Description)).ToList();
            return Result<string>.Failure(errors);
        }

        await _userManager.AddToRoleAsync(user, Roles.Operador.ToString());


        return Result<string>.Success(user.Id.ToString());
    }

    public async Task<Result<AuthenticationResponseDto>> AuthenticationAsync(LoginRequestDto request)
    {
        var ResultUserFind = await _userManager.FindByEmailAsync(request.Email);
        if (ResultUserFind == null)
        {
            return Result<AuthenticationResponseDto>.Failure(AccountErrors.InvalidCredentials);
        }

        var CheckPassword = await _signInManager.CheckPasswordSignInAsync(ResultUserFind, request.Password, lockoutOnFailure: false);

        if (!CheckPassword.Succeeded)
        {
            return Result<AuthenticationResponseDto>.Failure(AccountErrors.InvalidCredentials);

        }

    }

   
}
