using Microsoft.AspNetCore.Identity;

namespace PijacaUDzepu.API.Models;

public class User : IdentityUser<int>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<VendorUser> VendorUsers { get; set; } = new List<VendorUser>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
