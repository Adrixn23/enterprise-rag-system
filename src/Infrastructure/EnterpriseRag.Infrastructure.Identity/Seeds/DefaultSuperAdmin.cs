using Microsoft.AspNetCore.Identity;
using EnterpriseRag.Core.Domain.Enums;
using EnterpriseRag.Infrastructure.Identity.Entities;
namespace EnterpriseRag.Infrastructure.Identity.Seeds
{
    public static class DefaultSuperAdmin
    {
        public static  async Task SeedAsync(UserManager<ApplicationUser> userManager)
        {
            var defaultUser = new ApplicationUser
            {
                UserName = "superadmin",
                Email = "superadmin@rag.com",
                FullName = "Super Administrator",
                TenantId = Guid.NewGuid(),
                EmailConfirmed = true
            };
            var user = await userManager.FindByEmailAsync(defaultUser.Email);

            if (user == null)
            {
                await userManager.CreateAsync(defaultUser, "!Admin123");

                await userManager.AddToRoleAsync(defaultUser, Roles.SuperAdmin.ToString());
                await userManager.AddToRoleAsync(defaultUser, Roles.Admin.ToString());
                await userManager.AddToRoleAsync(defaultUser, Roles.Operador.ToString());
            }
        }

    }
}
