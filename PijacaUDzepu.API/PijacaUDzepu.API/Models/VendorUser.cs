namespace PijacaUDzepu.API.Models;

public class VendorUser
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
