using System.Text;
using EnterpriseRag.Core.Application.Contracts;
using EnterpriseRag.Core.Domain.Settings;
using EnterpriseRag.Infrastructure.Identity.Context;
using EnterpriseRag.Infrastructure.Identity.Entities;
using EnterpriseRag.Infrastructure.Identity.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;


namespace EnterpriseRag.IoC;

public static class InfrastructureIdentityDependencies
{
    public static IServiceCollection AddInfrastructureIdentityDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Vincular JWTSettings desde appsettings.json
        services.Configure<JWTSettings>(configuration.GetSection("JWTSettings"));

        // 2. Registrar el DbContext de Identity con SQL Server
        services.AddDbContext<IdentityContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("IdentityConnection"),
                b => b.MigrationsAssembly(typeof(IdentityContext).Assembly.FullName)));

        // 3. Registrar ASP.NET Core Identity
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireDigit = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
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
            options.RequireHttpsMetadata = false;
            options.SaveToken = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidIssuer = configuration["JWTSettings:Issuer"],
                ValidAudience = configuration["JWTSettings:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(configuration["JWTSettings:Key"] ?? "default_secret_key_for_development_only_12345!"))
            };
        });

        return services;
    }

    public static async Task SeedIdentityDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        try
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            await EnterpriseRag.Infrastructure.Identity.Seeds.DefaultRoles.SeedAsync(roleManager);
            await EnterpriseRag.Infrastructure.Identity.Seeds.DefaultSuperAdmin.SeedAsync(userManager);
        }
        catch (Exception ex)
        {
            var logger = services.GetService<Microsoft.Extensions.Logging.ILogger<IdentityContext>>();
            logger?.LogError(ex, "An error occurred while seeding default roles and super admin user.");
        }
    }
}


