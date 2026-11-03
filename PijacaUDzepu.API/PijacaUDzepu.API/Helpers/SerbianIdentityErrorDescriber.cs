using Microsoft.AspNetCore.Identity;

namespace PijacaUDzepu.API.Helpers;

// Poruke ASP.NET Identity-ja (lozinka, korisničko ime, token) na jeziku zahteva (vidi L), jer ih klijent prikazuje korisniku
public class SerbianIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = L.T("Lozinka mora imati najmanje {0} znakova.", length) };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = L.T("Lozinka mora imati bar jedno malo slovo.") };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = L.T("Trenutna lozinka nije ispravna.") };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = L.T("Korisničko ime je zauzeto.") };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = L.T("Korisničko ime sme da sadrži samo slova, brojeve i znakove - . _ @ +") };

    public override IdentityError InvalidToken() =>
        new() { Code = nameof(InvalidToken), Description = L.T("Link je istekao ili je već iskorišćen. Zatražite novi.") };
}
