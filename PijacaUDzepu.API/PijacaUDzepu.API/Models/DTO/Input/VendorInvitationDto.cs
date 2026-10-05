using System.ComponentModel.DataAnnotations;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class InviteVendorDto
{
    [Required]
    public int VendorId { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class AcceptInvitationDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(3)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MinLength(4)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;
}
