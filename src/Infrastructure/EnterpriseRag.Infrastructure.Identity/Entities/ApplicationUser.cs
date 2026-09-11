using Microsoft.AspNetCore.Identity;


namespace EnterpriseRag.Infrastructure.Identity.Entities
{
    public class ApplicationUser : IdentityUser<Guid> 
    {
        public string? FullName { get; set; }
        public Guid TenantId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; 

    }
}
