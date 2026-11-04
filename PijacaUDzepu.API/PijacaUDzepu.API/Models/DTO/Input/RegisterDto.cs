using System.ComponentModel.DataAnnotations;

namespace PijacaUDzepu.API.Models.DTO.Input;

public class RegisterDto
{
    [Required]
    [MinLength(3)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Lozinka mora imati najmanje 8 znakova.")]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    [Required(ErrorMessage = "Unesite broj telefona.")]
    [MaxLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Unesite adresu.")]
    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Floor { get; set; }

    [MaxLength(20)]
    public string? Apartment { get; set; }
}

// Telefon i adresa za dostavu (Moj nalog)
public class UpdateProfileDto
{
    [Required(ErrorMessage = "Unesite broj telefona.")]
    [MaxLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Address { get; set; }

    [MaxLength(20)]
    public string? Floor { get; set; }

    [MaxLength(20)]
    public string? Apartment { get; set; }
}

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Lozinka mora imati najmanje 8 znakova.")]
    public string NewPassword { get; set; } = string.Empty;
}

public class ForgotPasswordDto
{
    // Korisničko ime ili e-mail
    [Required]
    public string Login { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Lozinka mora imati najmanje 8 znakova.")]
    public string NewPassword { get; set; } = string.Empty;
}

public class DeleteAccountDto
{
    [Required]
    public string Password { get; set; } = string.Empty;
}
