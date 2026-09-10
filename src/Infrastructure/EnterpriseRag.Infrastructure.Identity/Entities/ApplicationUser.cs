using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseRag.Infrastructure.Identity.Entities
{
    public class ApplicationUser : IdentityUser<Guid> 
    {
        public string UserName;
        public Guid TenantId;

        public DateTime FechaCreacion; 

    }
}
