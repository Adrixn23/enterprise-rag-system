using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EnterpriseRag.Infrastructure.Identity.Seeds;

public static class DefaultSuperAdmin
{
    public static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        var email = configuration["IdentityInit:SuperAdminEmail"];
        var password = configuration["IdentityInit:SuperAdminPassword"];
        var tenantIdRaw = configuration["IdentityInit:SuperAdminTenantId"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            if (environment.IsDevelopment())
            {
                logger.LogWarning("Seed de SuperAdmin omitido: no se configuraron 'IdentityInit:SuperAdminEmail' o 'IdentityInit:SuperAdminPassword'.");
                return;
            }

            throw new InvalidOperationException(
                "Fallo crítico en inicialización: no se encontraron las credenciales requeridas 'IdentityInit:SuperAdminEmail' y 'IdentityInit:SuperAdminPassword' en el entorno de producción.");
        }

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            logger.LogInformation("El usuario SuperAdmin con email '{Email}' ya existe. No se realizaron cambios.", email);
            return;
        }

        Guid tenantId = Guid.TryParse(tenantIdRaw, out var parsedTenantId) && parsedTenantId != Guid.Empty
            ? parsedTenantId
            : Guid.NewGuid();

        var defaultUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Super Administrator",
            TenantId = tenantId,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(defaultUser, password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Error al crear el usuario SuperAdmin inicial: {errors}");
        }

        await userManager.AddToRoleAsync(defaultUser, Roles.SuperAdmin.ToString());
        await userManager.AddToRoleAsync(defaultUser, Roles.Admin.ToString());
        await userManager.AddToRoleAsync(defaultUser, Roles.Operador.ToString());

        logger.LogInformation("Usuario SuperAdmin creado exitosamente con email '{Email}' para el TenantId '{TenantId}'.", email, tenantId);
    }
}
