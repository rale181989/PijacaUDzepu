using System.Net.Http.Json;
using System.Net;

namespace PijacaUDzepu.API.Tests;

// Nalozi: zaključavanje posle pogrešnih lozinki, zaboravljena lozinka, brisanje naloga
[Collection("api")]
public class AccountTests
{
    private readonly ApiFactory _factory;

    public AccountTests(ApiFactory factory) => _factory = factory;

    private async Task<string> Register(string prefix)
    {
        var userName = $"{prefix}{Guid.NewGuid():N}"[..20];
        var api = await _factory.As();
        var res = await api.Send(HttpMethod.Post, "/api/auth/register", new
        {
            userName, password = "lozinka123", firstName = "Test", lastName = "Korisnik",
            email = $"{userName}@example.com", phoneNumber = "0600000000", address = "Dunavska 5"
        });
        res.EnsureSuccessStatusCode();
        return userName;
    }

    [Fact]
    public async Task Short_passwords_are_rejected()
    {
        var api = await _factory.As();
        var res = await api.Send(HttpMethod.Post, "/api/auth/register", new
        {
            userName = "kratka" + Random.Shared.Next(100000), password = "abc12", firstName = "A", lastName = "B", phoneNumber = "060", address = "Dunavska 5"
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    private record Profile(string? PhoneNumber, string? Address, string? Floor, string? Apartment);
    private record Auth(Profile User);

    [Fact]
    public async Task Registration_requires_phone_and_address_and_keeps_floor_and_apartment()
    {
        var api = await _factory.As();
        var noAddress = await api.Send(HttpMethod.Post, "/api/auth/register", new
        {
            userName = $"noaddr{Guid.NewGuid():N}"[..20], password = "lozinka123", firstName = "A", lastName = "B", phoneNumber = "0601234567"
        });
        Assert.Equal(HttpStatusCode.BadRequest, noAddress.StatusCode);
        Assert.Equal("Unesite adresu.", await Api.Message(noAddress));

        var userName = $"addr{Guid.NewGuid():N}"[..20];
        var res = await api.Send(HttpMethod.Post, "/api/auth/register", new
        {
            userName, password = "lozinka123", firstName = "A", lastName = "B",
            phoneNumber = " 0601234567 ", address = " Bulevar oslobođenja 10 ", floor = "3", apartment = ""
        });
        res.EnsureSuccessStatusCode();
        var created = (await res.Content.ReadFromJsonAsync<Auth>())!.User;
        Assert.Equal(new Profile("0601234567", "Bulevar oslobođenja 10", "3", null), created);

        var user = await _factory.As(userName, "lozinka123");
        var update = await user.Send(HttpMethod.Put, "/api/auth/profile", new { phoneNumber = "0611111111", address = "Dunavska 5", floor = "", apartment = "12" });
        update.EnsureSuccessStatusCode();
        Assert.Equal(new Profile("0611111111", "Dunavska 5", null, "12"), await user.Get<Profile>("/api/auth/me"));
    }

    [Fact]
    public async Task Account_locks_after_ten_wrong_passwords()
    {
        var userName = await Register("lock");
        var http = _factory.CreateClient();
        Task<HttpResponseMessage> Login(string password, string ip)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { userName, password }) };
            req.Headers.Add("X-Forwarded-For", ip);
            return http.SendAsync(req);
        }

        // Napadač sa jedne adrese: deseta pogrešna lozinka zaključava prijavu sa te adrese
        // (jedanaesti pokušaj bi ionako odbilo ograničenje od 10 prijava u minutu)
        for (var i = 0; i < 9; i++)
            await Login("pogresna" + i, "10.240.0.1");
        var attacker = await Login("pogresna9", "10.240.0.1");
        Assert.Equal(HttpStatusCode.Unauthorized, attacker.StatusCode);
        Assert.Contains("zaključan", await Api.Message(attacker));

        // Sa iste adrese ni ispravna lozinka ne prolazi (ni posle isteka minuta za ograničenje prijava)
        Assert.NotEqual(HttpStatusCode.OK, (await Login("lozinka123", "10.240.0.1")).StatusCode);

        // Vlasnik sa svog uređaja se i dalje prijavljuje: napadač ne može da mu zaključa nalog (nalaz V4)
        Assert.Equal(HttpStatusCode.OK, (await Login("lozinka123", "10.240.0.2")).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_answers_the_same_for_unknown_accounts()
    {
        var userName = await Register("forgot");
        var api = await _factory.As();
        var known = await api.Send(HttpMethod.Post, "/api/auth/forgot-password", new { login = userName });
        var unknown = await api.Send(HttpMethod.Post, "/api/auth/forgot-password", new { login = "nepostoji-" + Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(await Api.Message(known), await Api.Message(unknown));

        var invalid = await api.Send(HttpMethod.Post, "/api/auth/reset-password", new { token = "nije-pravi-token", newPassword = "novalozinka1" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Deleted_account_cannot_log_in_and_last_admin_cannot_be_deleted()
    {
        var userName = await Register("delete");
        var user = await _factory.As(userName, "lozinka123");
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Send(HttpMethod.Post, "/api/auth/delete-account", new { password = "pogresna1" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.Send(HttpMethod.Post, "/api/auth/delete-account", new { password = "lozinka123" })).StatusCode);

        var api = await _factory.As();
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Send(HttpMethod.Post, "/api/auth/login", new { userName, password = "lozinka123" })).StatusCode);

        var admin = await _factory.As("admin", "admin1234");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Send(HttpMethod.Post, "/api/auth/delete-account", new { password = "admin1234" })).StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_reveals_nothing_about_users()
    {
        var api = await _factory.As();
        var body = await (await api.Send(HttpMethod.Get, "/api/auth/health")).Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ok\"", body);
        Assert.DoesNotContain("admin", body);
    }
}
