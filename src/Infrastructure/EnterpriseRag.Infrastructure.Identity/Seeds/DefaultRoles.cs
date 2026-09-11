using Microsoft.AspNetCore.Identity;
 using EnterpriseRag.Core.Domain.Enums;

namespace EnterpriseRag.Infrastructure.Identity.Seeds
{
    public static class DefaultRoles
    {
        public static async Task SeedAsync(RoleManager<IdentityRole<Guid>> roleManager) {
            if (!await roleManager.RoleExistsAsync(Roles.SuperAdmin.ToString()))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.SuperAdmin.ToString()));
            }
            if (!await roleManager.RoleExistsAsync(Roles.Admin.ToString()))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Admin.ToString()));

            }
            if (!await roleManager.RoleExistsAsync(Roles.Operador.ToString()))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Operador.ToString()));

            }



        }



    }
}
