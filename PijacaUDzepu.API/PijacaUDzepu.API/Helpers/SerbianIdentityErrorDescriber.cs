using Microsoft.AspNetCore.Identity;

namespace PijacaUDzepu.API.Helpers;

// Poruke ASP.NET Identity-ja (lozinka, korisničko ime, token) na srpskom, jer ih klijent prikazuje korisniku
public class SerbianIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Lozinka mora imati najmanje {length} znakova." };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "Lozinka mora imati bar jedno malo slovo." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "Trenutna lozinka nije ispravna." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = "Korisničko ime je zauzeto." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "Korisničko ime sme da sadrži samo slova, brojeve i znakove - . _ @ +" };

    public override IdentityError InvalidToken() =>
        new() { Code = nameof(InvalidToken), Description = "Link je istekao ili je već iskorišćen. Zatražite novi." };
}
