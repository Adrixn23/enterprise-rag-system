using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EnterpriseRag.Core.Application.Contracts.Account;
using EnterpriseRag.Core.Application.Contracts.Email;
using EnterpriseRag.Core.Application.DTOs.Account;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors;
using EnterpriseRag.Core.Domain.Common.Errors.Account;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseRag.Infrastructure.Identity.Services;

public class AccountService : IAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JWTSettings _jwtSettings;
    private readonly IHostEnvironment _environment;
    private readonly IEmailSenderService _emailSender;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOptions<JWTSettings> jwtSettings,
        IHostEnvironment environment,
        IEmailSenderService emailSender,
        ILogger<AccountService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtSettings = jwtSettings.Value;
        _environment = environment;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<Result<string>> RegisterAsync(RegisterRequestDto request)
    {
        if (request.Password != request.ConfirmPassword)
        {
            return Result<string>.Failure(AccountErrors.PasswordsDoNotMatch);
        }

        var userWithSameEmail = await _userManager.FindByEmailAsync(request.Email);
        if (userWithSameEmail != null)
        {
            return Result<string>.Failure(AccountErrors.EmailAlreadyExists);
        }

        // SEGURIDAD MULTITENANT B2B ESTRICTA:
        // En el registro público de auto-servicio, NUNCA se confía en un TenantId proveniente del cliente.
        // Cada registro genera un nuevo TenantId criptográfico (Guid.NewGuid()) asignando una organización
        // completamente aislada. La incorporación a una organización existente debe ocurrir mediante
        // un flujo autorizado de invitación gestionado por el administrador del tenant.
        var assignedTenantId = Guid.NewGuid();

        // En Development se permite auto-confirmación para pruebas locales ágiles.
        // En Production, EmailConfirmed inicia estrictamente en false y requiere confirmación por token.
        var isDevelopment = _environment.IsDevelopment();

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            TenantId = assignedTenantId,
            EmailConfirmed = isDevelopment
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => new Error(e.Code, e.Description)).ToList();
            return Result<string>.Failure(errors);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, Roles.Operador.ToString());
        if (!roleResult.Succeeded)
        {
            _logger.LogWarning("Falló la asignación del rol al usuario {UserId}. Ejecutando compensación...", user.Id);
            
            // Compensación atómica: eliminar el usuario recién creado
            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                _logger.LogCritical("CRITICAL: Falló la compensación de eliminación para el usuario {UserId}. Estado inconsistente en Identity.", user.Id);
                return Result<string>.Failure(AccountErrors.InconsistentUserState);
            }

            var roleErrors = roleResult.Errors.Select(e => new Error(e.Code, e.Description)).ToList();
            return Result<string>.Failure(roleErrors);
        }

        // En producción, generar token y despachar enlace de verificación
        if (!isDevelopment)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = $"/api/v1/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";
            await _emailSender.SendEmailConfirmationAsync(user.Email, confirmationLink);
        }

        return Result<string>.Success(user.Id.ToString());
    }

    public async Task<Result> ConfirmEmailAsync(Guid userId, string token)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure(AccountErrors.InvalidConfirmationToken);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.Failure(AccountErrors.InvalidConfirmationToken);
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return Result.Failure(AccountErrors.InvalidConfirmationToken);
        }

        return Result.Success();
    }

    public async Task<Result<AuthenticationResponseDto>> AuthenticationAsync(LoginRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Result<AuthenticationResponseDto>.Failure(AccountErrors.InvalidCredentials);
        }

        // En entornos que no sean Development, exigir correo confirmado antes de permitir el login
        if (!_environment.IsDevelopment() && !user.EmailConfirmed)
        {
            return Result<AuthenticationResponseDto>.Failure(AccountErrors.EmailNotConfirmed);
        }

        // Bloqueo habilitado contra ataques de fuerza bruta (lockoutOnFailure: true)
        var checkPassword = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (checkPassword.IsLockedOut)
        {
            return Result<AuthenticationResponseDto>.Failure(AccountErrors.AccountLocked);
        }

        if (!checkPassword.Succeeded)
        {
            return Result<AuthenticationResponseDto>.Failure(AccountErrors.InvalidCredentials);
        }

        var userRoles = await _userManager.GetRolesAsync(user);
        var token = GenerateJwtToken(user, userRoles);

        var response = new AuthenticationResponseDto
        {
            Id = user.Id,
            FullName = user.FullName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Roles = userRoles.ToList(),
            Token = token,
            IsAuthenticated = true
        };

        return Result<AuthenticationResponseDto>.Success(response);
    }

    private string GenerateJwtToken(ApplicationUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenantId", user.TenantId.ToString()),
            new("fullName", user.FullName ?? string.Empty)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
