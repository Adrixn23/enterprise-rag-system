using System.Text;
using EnterpriseRag.Core.Application.Contracts.Account;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.Identity.Context;
using EnterpriseRag.Infrastructure.Identity.Entities;
using EnterpriseRag.Infrastructure.Identity.Seeds;
using EnterpriseRag.Infrastructure.Identity.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseRag.IoC;

public static class InfrastructureIdentityDependencies
{
    public static IServiceCollection AddInfrastructureIdentityDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Vincular y validar JWTSettings al inicio de la aplicación (Fail-Fast)
        services.AddOptions<JWTSettings>()
            .Bind(configuration.GetSection(JWTSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(jwt => jwt.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(jwt)).Any() == false,
                "La configuración de JWTSettings no superó la validación criptográfica.")
            .ValidateOnStart();

        // 2. Registrar el DbContext de Identity con SQL Server
        services.AddDbContext<IdentityContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("IdentityConnection"),
                b => b.MigrationsAssembly(typeof(IdentityContext).Assembly.FullName)));

        // 3. Registrar ASP.NET Core Identity con políticas de contraseñas y bloqueo robustas
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            // Políticas de contraseña reforzadas
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;

            // Bloqueo de cuenta contra ataques de fuerza bruta
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<IdentityContext>()
        .AddDefaultTokenProviders();

        // 4. Registrar la inyección de dependencias para IAccountService
        services.AddTransient<IAccountService, AccountService>();

        // 5. Configurar la autenticación JWT Bearer
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // Se recupera la configuración ya validada
            var jwtKey = configuration[$"{JWTSettings.SectionName}:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
            {
                throw new InvalidOperationException("No se puede iniciar el middleware de autenticación: la clave JWT no cumple con el requisito mínimo de 32 bytes.");
            }

            options.RequireHttpsMetadata = false;
            options.SaveToken = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidIssuer = configuration[$"{JWTSettings.SectionName}:Issuer"],
                ValidAudience = configuration[$"{JWTSettings.SectionName}:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };
        });

        return services;
    }

    public static async Task SeedIdentityDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<IdentityContext>>();

        try
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var configuration = services.GetRequiredService<IConfiguration>();
            var environment = services.GetRequiredService<IHostEnvironment>();

            await DefaultRoles.SeedAsync(roleManager);
            await DefaultSuperAdmin.SeedAsync(userManager, configuration, environment, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocurrió un error crítico durante el sembrado de roles y super admin.");
            throw; // Propagar para que el inicio de la aplicación falle si el seed obligatorio de producción falla
        }
    }
}
