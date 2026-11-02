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
    public string? PhoneNumber { get; set; }
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
