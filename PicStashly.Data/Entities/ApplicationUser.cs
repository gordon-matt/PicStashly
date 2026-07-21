using Microsoft.AspNetCore.Identity;

namespace PicStashly.Data.Entities;

public class ApplicationUser : IdentityUser
{
    public virtual ICollection<ApplicationRole> Roles { get; set; } = [];
}