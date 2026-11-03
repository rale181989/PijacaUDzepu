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
            email = $"{userName}@example.com", phoneNumber = "0600000000"
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
            userName = "kratka" + Random.Shared.Next(100000), password = "abc12", firstName = "A", lastName = "B", phoneNumber = "060"
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Account_locks_after_ten_wrong_passwords()
    {
        var userName = await Register("lock");
        var api = await _factory.As();
        for (var i = 0; i < 10; i++)
            await api.Send(HttpMethod.Post, "/api/auth/login", new { userName, password = "pogresna" + i });

        var res = await api.Send(HttpMethod.Post, "/api/auth/login", new { userName, password = "lozinka123" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Contains("zaključan", await Api.Message(res));
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
