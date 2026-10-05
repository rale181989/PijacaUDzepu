using Microsoft.AspNetCore.Identity;

namespace PijacaUDzepu.API.Models;

public class Role : IdentityRole<int>
{
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
