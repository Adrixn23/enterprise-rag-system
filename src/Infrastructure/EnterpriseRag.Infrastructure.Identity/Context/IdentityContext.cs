using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EnterpriseRag.Infrastructure.Identity.Entities;

namespace EnterpriseRag.Infrastructure.Identity.Context
{
    public class IdentityContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public IdentityContext(DbContextOptions<IdentityContext> options) : base(options) { }
    }   
}
