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
using Microsoft.Extensions.Caching.Memory;
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
    private readonly EmailSettings _emailSettings;
    private readonly IHostEnvironment _environment;
    private readonly IEmailSenderService _emailSender;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AccountService> _logger;

    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(2);
    private static readonly SemaphoreSlim[] ResendLocks = Enumerable
        .Range(0, 64)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    public AccountService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOptions<JWTSettings> jwtSettings,
        IOptions<EmailSettings> emailSettings,
        IHostEnvironment environment,
        IEmailSenderService emailSender,
        IMemoryCache memoryCache,
        ILogger<AccountService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtSettings = jwtSettings.Value;
        _emailSettings = emailSettings.Value;
        _environment = environment;
        _emailSender = emailSender;
        _memoryCache = memoryCache;
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

        // En producción, generar token y despachar enlace de verificación con base URL absoluta validada
        // El token viaja en el fragmento (#token=...) para que nunca sea enviado al servidor en el GET.
        if (!isDevelopment)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = BuildConfirmationLink(user.Id, token);

            try
            {
                await _emailSender.SendEmailConfirmationAsync(user.Email, confirmationLink);
            }
            catch (Exception ex)
            {
                // NO se elimina el usuario si falla el transporte de correo (evita pérdida de cuenta).
                // Se permite reintentar el reenvío mediante /resend-confirmation.
                _logger.LogError(ex, "Fallo al despachar correo de confirmación a {Email}. El usuario puede solicitar reenvío.", user.Email);
            }
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

        if (user.EmailConfirmed)
        {
            // Token ya utilizado o cuenta ya confirmada previamente: evitar re-ejecuciones
            return Result.Failure(AccountErrors.InvalidConfirmationToken);
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return Result.Failure(AccountErrors.InvalidConfirmationToken);
        }

        return Result.Success();
    }

    public async Task<Result> ResendConfirmationEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            // Respuesta defensiva: éxito simulado para prevenir enumeración de usuarios
            return Result.Success();
        }

        // Cooldown por cuenta en memoria local (single-instance) con bloqueo particionado para evitar
        // condiciones de carrera (TOCTOU) ante solicitudes simultáneas.
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var lockIndex = (normalizedEmail.GetHashCode(StringComparison.Ordinal) & 0x7FFFFFFF) % ResendLocks.Length;
        var accountLock = ResendLocks[lockIndex];

        await accountLock.WaitAsync();
        try
        {
            var cooldownKey = $"auth:resend-cooldown:{normalizedEmail}";
            if (_memoryCache.TryGetValue(cooldownKey, out _))
            {
                _logger.LogInformation("Solicitud de reenvío omitida por cooldown activo.");
                return Result.Success();
            }

            var user = await _userManager.FindByEmailAsync(email.Trim());
            if (user == null || user.EmailConfirmed)
            {
                // Protección contra User Enumeration Attack: registrar cooldown y responder éxito idéntico
                _memoryCache.Set(cooldownKey, true, ResendCooldown);
                return Result.Success();
            }

            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = BuildConfirmationLink(user.Id, token);
                await _emailSender.SendEmailConfirmationAsync(user.Email!, confirmationLink);

                // Establecer cooldown únicamente tras un envío exitoso (si falla SMTP, el usuario puede reintentar)
                _memoryCache.Set(cooldownKey, true, ResendCooldown);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo al reenviar correo de confirmación a {Email}.", email);
            }

            return Result.Success();
        }
        finally
        {
            accountLock.Release();
        }
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

    private string BuildConfirmationLink(Guid userId, string token)
    {
        var baseUrl = _emailSettings.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/api/v1/auth/confirm-email?userId={userId}#token={Uri.EscapeDataString(token)}";
    }
}
